using System.Linq;
using Typedown.Automation;

namespace Typedown.Services.Automation
{
    /// <summary>
    /// What only the automation test host adds: the edit barriers and the test.* methods (Typedown.Automation.TestHost).
    /// The application build compiles the empty half and never references that assembly; packaging refuses any
    /// output that contains it (Tools/Installer/assert-application-build.ps1).
    /// </summary>
    internal static class TestHostHooks
    {
#if AUTOMATION_TEST_HOST
        private static readonly Typedown.Automation.TestHost.EditBarriers barriers = new();

        public static IEditBarriers Barriers => barriers;

        private static readonly System.Collections.Generic.List<object> keptWindows = new();

        private struct NativePoint { public int X, Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ClientToScreen(System.IntPtr window, ref NativePoint point);

        public static void AddMethods(MethodTable methods)
        {
            barriers.AddMethods(methods);
            // What the page itself holds right now - lets a test wait for real input to arrive instead of sleeping.
            methods.Add(new MethodDescriptor("test.editor.pageText", null, "test.editor.pageText/1", async (c, ct) =>
            {
                var documentId = c.Params.RequiredString("documentId", allowEmpty: false);
                var found = await Core.Services.AutomationWindows.FindDocumentAsync(documentId)
                    ?? throw new AutomationException(AutomationErrorKind.document_not_found, "no such document");
                var text = await await Core.Services.AutomationWindows.Registry.OnWindowAsync(found.Window.WindowId, app =>
                    app.TabsViewModel.ActiveTab == found.Tab ? app.EditorViewModel.ReadPageTextAsync(5000) : System.Threading.Tasks.Task.FromResult<string>(null));
                return new Newtonsoft.Json.Linq.JObject { ["text"] = text };
            }));

            // The window's UI thread runs every callback posted to it, from any number of threads at once. Its dispatcher
            // numbered posts with a plain increment: two threads could take the same number, and one callback was
            // dropped - an await continuation that never ran (E2E C01 hung a minute with the UI thread idle).
            methods.Add(new MethodDescriptor("test.dispatcher.stress", null, "test.dispatcher.stress/1", async (c, ct) =>
            {
                var windowId = c.Params.RequiredString("windowId", allowEmpty: false);
                var threads = (int)(c.Params.OptionalInteger("threads") ?? 8);
                var posts = (int)(c.Params.OptionalInteger("posts") ?? 20000);
                var context = await Core.Services.AutomationWindows.Registry.OnWindowAsync(windowId, app => System.Threading.SynchronizationContext.Current);
                var ran = 0;
                var start = new System.Threading.ManualResetEventSlim();
                var senders = Enumerable.Range(0, threads).Select(_ => System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    for (var i = 0; i < posts; i++) context.Post(__ => System.Threading.Interlocked.Increment(ref ran), null);
                })).ToArray();
                start.Set();
                await System.Threading.Tasks.Task.WhenAll(senders);
                var total = threads * posts;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (System.Threading.Volatile.Read(ref ran) < total && clock.ElapsedMilliseconds < 20000) await System.Threading.Tasks.Task.Delay(50);
                return new Newtonsoft.Json.Linq.JObject { ["posted"] = total, ["ran"] = System.Threading.Volatile.Read(ref ran), ["context"] = context?.GetType().FullName };
            }));

            // Backups: one pass now in every window, and what the backup folder holds afterwards.
            methods.Add(new MethodDescriptor("test.backup.run", null, "test.backup.run/1", async (c, ct) =>
            {
                var report = new Newtonsoft.Json.Linq.JArray();
                foreach (var window in Core.Services.AutomationWindows.Registry.Snapshot())
                {
                    var entry = await await Core.Services.AutomationWindows.Registry.OnWindowAsync(window.WindowId, async app =>
                    {
                        var busy = app.FileViewModel.SaveTimerBusy;
                        try { return new Newtonsoft.Json.Linq.JObject { ["windowId"] = window.WindowId, ["ok"] = await app.FileViewModel.BackupNowAsync(), ["timerBusy"] = busy }; }
                        catch (System.Exception e) { return new Newtonsoft.Json.Linq.JObject { ["windowId"] = window.WindowId, ["error"] = e.ToString(), ["timerBusy"] = busy }; }
                    });
                    report.Add(entry);
                }
                var folder = System.IO.Path.Combine(Core.Config.GetLocalFolderPath(), "Backup");
                var files = System.IO.Directory.Exists(folder) ? System.IO.Directory.GetFiles(folder).Select(System.IO.Path.GetFileName).ToArray() : new string[0];
                return new Newtonsoft.Json.Linq.JObject { ["windows"] = report, ["files"] = new Newtonsoft.Json.Linq.JArray(files) };
            }));

            // How a window's editor page is drawn: its font size, line height and text direction, as the page reports them.
            methods.Add(new MethodDescriptor("test.editor.style", null, "test.editor.style/1", async (c, ct) =>
                await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    if (app.MarkdownEditor == null) throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window has no editor page (another page is shown)");
                    return await app.EditorViewModel.QueryEditorStyleAsync(5000)
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the editor page did not answer within 5 s");
                })));
            // Keyboard focus into the window's editor page, the way the app itself gives it (the editor control's
            // focus is handed on to the web view) - independent of what a click would land on.
            methods.Add(new MethodDescriptor("test.editor.focus", null, "test.editor.focus/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var editor = app.MarkdownEditor as global::Windows.UI.Xaml.Controls.Control
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window shows no editor page");
                    (editor as Core.Interfaces.IMarkdownEditor)!.FocusEditor();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["focused"] = editor.FocusState != global::Windows.UI.Xaml.FocusState.Unfocused };
                })));
            // Runs a script in the window's editor page and returns its JSON result: for diagnosing what the page holds
            // (caret, Muya's state) when a check fails in the real window and not in the page harness.
            methods.Add(new MethodDescriptor("test.editor.eval", null, "test.editor.eval/1", async (c, ct) =>
                await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    var editor = app.MarkdownEditor as Typedown.Controls.MarkdownEditor
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window shows no editor page");
                    var json = await editor.CoreWebView2.ExecuteScriptAsync(c.Params.RequiredString("script"));
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["result"] = Newtonsoft.Json.Linq.JToken.Parse(json) };
                })));
            // Where a point of the editor page (CSS pixels, as getBoundingClientRect gives them) is on the screen, in physical
            // pixels: the page's coordinates are the editor control's (the floating tools are placed by the same mapping),
            // scaled by the window's rasterization scale from its client area. The web view draws into the window without a
            // window or an automation element of its own, so a test that clicks the page has nothing else to measure.
            methods.Add(new MethodDescriptor("test.editor.screenPoint", null, "test.editor.screenPoint/1", (c, ct) =>
            {
                var x = (double)(c.Params.OptionalInteger("x") ?? 0);
                var y = (double)(c.Params.OptionalInteger("y") ?? 0);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var editor = app.MarkdownEditor as global::Windows.UI.Xaml.UIElement
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window has no editor page (another page is shown)");
                    var root = app.XamlRoot?.Content as global::Windows.UI.Xaml.UIElement
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window has no content");
                    var inRoot = editor.TransformToVisual(root).TransformPoint(new global::Windows.Foundation.Point(x, y));
                    var scale = app.XamlRoot.RasterizationScale;
                    var point = new NativePoint { X = (int)System.Math.Round(inRoot.X * scale), Y = (int)System.Math.Round(inRoot.Y * scale) };
                    ClientToScreen(app.MainWindow, ref point);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["x"] = point.X, ["y"] = point.Y, ["scale"] = scale };
                });
            }));
            // A state an edition takes from outside (Edition.TestSet), set for a test; an error when the edition has no
            // such name (Typedown has none).
            methods.Add(new MethodDescriptor("test.edition.set", null, "test.edition.set/1", (c, ct) =>
            {
                var name = c.Params.RequiredString("name", allowEmpty: false);
                var value = c.Params.OptionalString("value") ?? "";
                var handled = false;
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    handled = Edition.TrySet(app, name, value);
                    if (!handled) throw Params.Invalid("name", "unknown");
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // A state of the edition's (Edition.TestGet), as a string; an error when the edition has no such name.
            methods.Add(new MethodDescriptor("test.edition.get", null, "test.edition.get/1", (c, ct) =>
            {
                var name = c.Params.RequiredString("name", allowEmpty: false);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var value = Edition.TryGet(app, name) ?? throw Params.Invalid("name", "unknown");
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["value"] = value };
                });
            }));
            // Reloads the window's editor page, as the application does after a page error or a crashed web process.
            methods.Add(new MethodDescriptor("test.editor.reload", null, "test.editor.reload/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var editor = app.MarkdownEditor as Typedown.Controls.MarkdownEditor
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "the window shows no editor page");
                    editor.CoreWebView2.Reload();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                })));
            // The window's native handle (HWND), for UI Automation and window messages from the driver.
            methods.Add(new MethodDescriptor("test.window.handle", null, "test.window.handle/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                    (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["hwnd"] = app.MainWindow.ToInt64() })));
            // The titles of the window's menu bar, as drawn: the menu follows the interface language.
            methods.Add(new MethodDescriptor("test.window.menuTitles", null, "test.window.menuTitles/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var titles = new Newtonsoft.Json.Linq.JArray();
                    void Walk(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        // The menus shown: Paragraph and Format are hidden in source mode.
                        if (node is global::Microsoft.UI.Xaml.Controls.MenuBarItem item && item.Visibility == global::Windows.UI.Xaml.Visibility.Visible) titles.Add(item.Title);
                        var count = global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
                        for (var i = 0; i < count; i++) Walk(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    if (app.XamlRoot?.Content is global::Windows.UI.Xaml.DependencyObject root) Walk(root);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["titles"] = titles };
                })));
            // Where the main page starts in its window, and whether the window is in full screen: in full screen nothing may
            // sit above it (a 4 px row of window background did, along the top of the screen).
            methods.Add(new MethodDescriptor("test.window.layout", null, "test.window.layout/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var root = app.XamlRoot?.Content as global::Windows.UI.Xaml.UIElement;
                    global::Windows.UI.Xaml.FrameworkElement? page = null;
                    void Walk(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (page != null) return;
                        if (node is Core.Pages.MainPage found) { page = found; return; }
                        var count = global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
                        for (var i = 0; i < count; i++) Walk(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    if (root != null) Walk(root);
                    var top = page != null && root != null ? page.TransformToVisual(root).TransformPoint(new global::Windows.Foundation.Point(0, 0)).Y : double.NaN;
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject
                    {
                        ["fullScreen"] = app.UIViewModel.IsFullScreen,
                        ["mainPageTop"] = top,
                        ["rootHeight"] = (root as global::Windows.UI.Xaml.FrameworkElement)?.ActualHeight ?? double.NaN,
                    };
                })));
            // Holds a window in memory from now on, as anything still referring to it does after it closes:
            // XamlWindow.AllWindows lists a window until the garbage collector has finalized it, closed or not.
            methods.Add(new MethodDescriptor("test.window.keep", null, "test.window.keep/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var window = global::Typedown.XamlUI.XamlWindow.AllWindows.OfType<Typedown.Windows.MainWindow>().FirstOrDefault(w => w.Handle == app.MainWindow)
                        ?? throw new AutomationException(AutomationErrorKind.window_not_found, "no main window with that handle");
                    keptWindows.Add(window);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                })));
            // Moves a window to a page, as its menu would: route "Settings/Editor" opens that settings page, "Main" goes back.
            methods.Add(new MethodDescriptor("test.window.navigate", null, "test.window.navigate/1", (c, ct) =>
            {
                var route = c.Params.RequiredString("route", allowEmpty: false);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    app.NavigateCommand.Execute(route);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));

            // Opens another window, as File > New window does, and returns its windowId once it is registered.
            methods.Add(new MethodDescriptor("test.window.open", null, "test.window.open/1", async (c, ct) =>
            {
                var before = Core.Services.AutomationWindows.Registry.Snapshot().Select(w => w.WindowId).ToList();
                var first = Core.Services.AutomationWindows.Registry.Snapshot().First();
                await Core.Services.AutomationWindows.Registry.OnWindowAsync(first.WindowId, _ => Utilities.Common.OpenNewWindow(new string[0], forceNewWindow: true));
                for (var i = 0; i < 200; i++)
                {
                    var added = Core.Services.AutomationWindows.Registry.Snapshot().FirstOrDefault(w => !before.Contains(w.WindowId));
                    if (added != null) return new Newtonsoft.Json.Linq.JObject { ["windowId"] = added.WindowId };
                    await System.Threading.Tasks.Task.Delay(50, ct);
                }
                throw new AutomationException(AutomationErrorKind.editor_not_ready, "no new window registered");
            }));
            // Sets a property of a window's settings view model by name, as its settings page would: also the settings the
            // automation API does not expose.
            methods.Add(new MethodDescriptor("test.settings.set", null, "test.settings.set/1", (c, ct) =>
            {
                var p = c.Params;
                var name = p.RequiredString("name", allowEmpty: false);
                var value = (c.Request.Params as Newtonsoft.Json.Linq.JObject)?["value"];
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(p.RequiredString("windowId"), app =>
                {
                    var property = app.SettingsViewModel.GetType().GetProperty(name) ?? throw Params.Invalid("name", "unknown");
                    property.SetValue(app.SettingsViewModel, value!.ToObject(property.PropertyType));
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // Reads a property of a window's settings view model by name.
            methods.Add(new MethodDescriptor("test.settings.get", null, "test.settings.get/1", (c, ct) =>
            {
                var p = c.Params;
                var name = p.RequiredString("name", allowEmpty: false);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(p.RequiredString("windowId"), app =>
                {
                    var property = app.SettingsViewModel.GetType().GetProperty(name) ?? throw Params.Invalid("name", "unknown");
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["value"] = Newtonsoft.Json.Linq.JToken.FromObject(property.GetValue(app.SettingsViewModel)) };
                });
            }));

            // Image upload without its dialogs: an enabled configuration (PowerShell or S3), chosen as Settings > Image >
            // Upload with; returns what was stored, so a test can see that the secret is not there in plain text.
            methods.Add(new MethodDescriptor("test.images.configure", null, "test.images.configure/1", async (c, ct) =>
            {
                var p = c.Params;
                var method = p.OptionalEnum("method", "powershell", "powershell", "s3");
                var settings = p.OptionalObject("config") ?? new Newtonsoft.Json.Linq.JObject();
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(p.RequiredString("windowId"), async app =>
                {
                    var upload = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Core.Services.ImageUpload>(app.ServiceProvider);
                    var config = await upload.AddImageUploadConfig("e2e-" + method, method == "s3" ? Core.Enums.ImageUploadMethod.OSS : Core.Enums.ImageUploadMethod.PowerShell);
                    var enabledOnCreate = config.IsEnable;
                    config.IsEnable = true;
                    var model = config.LoadUploadConfig();
                    if (model is Core.Models.UploadConfigModels.PowerShellModel ps)
                        ps.Script = (string)settings["script"] ?? ps.Script;
                    else if (model is Core.Models.UploadConfigModels.OSSConfigModel s3)
                    {
                        s3.ServiceURL = (string)settings["endpoint"] ?? "";
                        s3.RegionEndpoint = (string)settings["region"] ?? "";
                        s3.BucketName = (string)settings["bucket"] ?? "";
                        s3.AccessKey = (string)settings["accessKey"] ?? "";
                        s3.SecretKey = (string)settings["secretKey"] ?? "";
                        s3.PathStyle = (bool?)settings["pathStyle"] ?? false;
                        s3.UploadPath = (string)settings["prefix"] ?? "";
                        s3.ExternalURL = (string)settings["publicUrl"] ?? "";
                    }
                    config.StoreUploadConfig(model);
                    await upload.SaveImageUploadConfig(config);
                    app.SettingsViewModel.DefaultImageUploadConfigId = config.Id;
                    // What the configuration holds: the secret must not be there in plain text.
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["id"] = config.Id, ["stored"] = config.Config, ["default"] = upload.DefaultConfig?.Id, ["enabledOnCreate"] = enabledOnCreate };
                });
            }));
            // What a drop, paste or pick of image files does: each through the local image setting, inserted together.
            methods.Add(new MethodDescriptor("test.images.insert", null, "test.images.insert/1", async (c, ct) =>
            {
                var paths = c.Params.OptionalStringArray("paths") ?? throw Params.Invalid("paths", "required");
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    await app.EditorViewModel.InsertLocalImagesAsync(paths.ToList());
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // A picture file pasted as a bitmap (a screenshot on the clipboard): the clipboard image action's result, the
            // address the editor would insert - without the clipboard itself, which other programs on the machine share.
            methods.Add(new MethodDescriptor("test.images.paste", null, "test.images.paste/1", async (c, ct) =>
            {
                var path = c.Params.RequiredString("path", allowEmpty: false);
                var file = await System.WindowsRuntimeSystemExtensions.AsTask(global::Windows.Storage.StorageFile.GetFileFromPathAsync(path));
                var image = new Utilities.ClipboardImage(global::Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    var action = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Core.Services.ImageAction>(app.ServiceProvider);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["address"] = await action.DoClipboardAction(image) };
                });
            }));
            // A theme picked as the View menu picks it (a custom theme brings its base light/dark along), and what the
            // window then draws with.
            methods.Add(new MethodDescriptor("test.theme.apply", null, "test.theme.apply/1", async (c, ct) =>
            {
                var id = c.Params.OptionalString("customTheme") ?? "";
                var builtIn = c.Params.OptionalEnum("builtIn", "Default", "Default", "Light", "Dark", "Black");
                return await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    if (id.Length > 0)
                        app.SettingsViewModel.ApplyCustomTheme(Core.Utilities.ThemeFiles.Find(id) ?? throw Params.Invalid("customTheme", "unknown"));
                    else
                        app.SettingsViewModel.ApplyBuiltInTheme((Core.Enums.AppTheme)System.Enum.Parse(typeof(Core.Enums.AppTheme), builtIn));
                    var root = app.XamlRoot?.Content as global::Windows.UI.Xaml.FrameworkElement;
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["appTheme"] = app.SettingsViewModel.AppTheme.ToString(), ["actualTheme"] = root?.ActualTheme.ToString() };
                });
            }));
            // View > Theme: the entries it shows, after clicking its "Reload themes" item when asked (through the item's
            // automation peer, as an assistive tool would).
            methods.Add(new MethodDescriptor("test.theme.menu", null, "test.theme.menu/1", (c, ct) =>
            {
                var click = c.Params.OptionalBoolean("reload", false);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    Core.Controls.EditorControls.MenuBarItems.ViewItem? view = null;
                    void Find(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (view != null || node == null) return;
                        if (node is Core.Controls.EditorControls.MenuBarItems.ViewItem v) { view = v; return; }
                        for (var i = 0; i < global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                            Find(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    Find(app.XamlRoot?.Content as global::Windows.UI.Xaml.DependencyObject);
                    if (view == null) throw new AutomationException(AutomationErrorKind.editor_not_ready, "no View menu in this window");
                    var (entries, reload) = view.ThemeMenu();
                    if (click && reload != null)
                        ((global::Windows.UI.Xaml.Automation.Provider.IInvokeProvider)new global::Windows.UI.Xaml.Automation.Peers.MenuFlyoutItemAutomationPeer(reload)).Invoke();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["entries"] = new Newtonsoft.Json.Linq.JArray(entries), ["reload"] = reload?.Text, ["folder"] = Core.Utilities.ThemeFiles.Folder,
                        ["actualTheme"] = (app.XamlRoot?.Content as global::Windows.UI.Xaml.FrameworkElement)?.ActualTheme.ToString() };
                });
            }));
            // The next loads of the window's editor come back rewritten, as a load that did not keep the text did once.
            methods.Add(new MethodDescriptor("test.editor.rewriteLoads", null, "test.editor.rewriteLoads/1", (c, ct) =>
            {
                var count = (int)(c.Params.OptionalInteger("count") ?? 1);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    app.EditorViewModel.RewriteLoadsForTest = count;
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // Presses a button of the dialog open in the window (primary, secondary or close), as a click would; the
            // button's text back, with the dialog's title and its message when they are text. A test finding it through
            // UI Automation found the window's own close button first.
            methods.Add(new MethodDescriptor("test.dialog.press", null, "test.dialog.press/1", (c, ct) =>
            {
                var which = c.Params.OptionalEnum("button", "close", "primary", "secondary", "close");
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    Core.Controls.AppContentDialog? dialog = null;
                    void Find(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (node == null) return;
                        if (node is Core.Controls.AppContentDialog d && d.IsLoaded) dialog = d;
                        for (var i = 0; i < global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                            Find(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    Find(app.XamlRoot?.Content as global::Windows.UI.Xaml.DependencyObject);
                    if (dialog == null) throw new AutomationException(AutomationErrorKind.editor_not_ready, "no dialog is open in this window");
                    var button = which == "primary" ? dialog.PrimaryButton : which == "secondary" ? dialog.SecondaryButton : dialog.CloseButton;
                    if (button == null || button.Visibility != global::Windows.UI.Xaml.Visibility.Visible || !button.IsEnabled)
                        throw new AutomationException(AutomationErrorKind.editor_not_ready, $"the dialog has no {which} button to press");
                    ((global::Windows.UI.Xaml.Automation.Provider.IInvokeProvider)new global::Windows.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button)).Invoke();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject
                    {
                        ["pressed"] = button.Content?.ToString(),
                        ["title"] = dialog.Title as string,
                        ["content"] = dialog.Content as string,
                    };
                });
            }));
            // Settings > Image > Insert web image: kept on the web ("none"), copied to a folder ("copy", relative to the
            // document) or uploaded ("upload").
            methods.Add(new MethodDescriptor("test.images.webAction", null, "test.images.webAction/1", (c, ct) =>
            {
                var action = c.Params.OptionalEnum("action", "none", "none", "copy", "upload");
                var path = c.Params.OptionalString("path");
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    app.SettingsViewModel.InsertWebImageAction = action == "copy" ? Core.Enums.InsertImageAction.CopyToPath
                        : action == "upload" ? Core.Enums.InsertImageAction.Upload : Core.Enums.InsertImageAction.None;
                    if (path != null) app.SettingsViewModel.InsertWebImageCopyPath = path;
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // Text and HTML pasted into the window's active document as from the clipboard (the paste after the clipboard
            // is read: the page converts the HTML, then web pictures go as the setting above says); answers once done.
            methods.Add(new MethodDescriptor("test.paste.html", null, "test.paste.html/1", async (c, ct) =>
            {
                var html = c.Params.RequiredString("html", allowEmpty: false);
                var text = c.Params.OptionalString("text") ?? "";
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    await app.EditorViewModel.PasteTextAsync("normal", text, html);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                });
            }));
            // The window's editor messages with a handler that throws on the first one: how many each of two handlers got
            // of two messages (a handler that threw was detached for good, and the message went no further).
            methods.Add(new MethodDescriptor("test.events.throwingHandler", null, "test.events.throwingHandler/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var center = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Core.Services.EventCenter>(app.ServiceProvider);
                    int throwing = 0, other = 0;
                    var name = "E2E.Probe." + System.Guid.NewGuid().ToString("N");
                    using (System.ObservableExtensions.Subscribe(center.GetObservable<object>(name), _ => { throwing++; if (throwing == 1) throw new System.InvalidOperationException("e2e: a handler that throws"); }))
                    using (System.ObservableExtensions.Subscribe(center.GetObservable<object>(name), _ => other++))
                    {
                        try { center.EmitEvent(name, null); } catch (System.Exception ex) { Core.Utilities.Log.Debug($"e2e: the message failed: {ex.Message}"); }
                        try { center.EmitEvent(name, null); } catch (System.Exception ex) { Core.Utilities.Log.Debug($"e2e: the message failed: {ex.Message}"); }
                    }
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["throwing"] = throwing, ["other"] = other };
                })));
            // An exception out of an event handler XAML calls (a timer's, as a click handler's would be): the process stays
            // (it ended the app), and the exception is in the log.
            methods.Add(new MethodDescriptor("test.app.throwUnhandled", null, "test.app.throwUnhandled/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var timer = new global::Windows.UI.Xaml.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(50) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        throw new System.InvalidOperationException("e2e: an exception nothing catches, out of an event handler");
                    };
                    timer.Start();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
                })));
            // The popups open in a window (menus, flyouts, a number box's buttons...), by the type of what they show.
            methods.Add(new MethodDescriptor("test.window.popups", null, "test.window.popups/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var open = app.XamlRoot == null ? new System.Collections.Generic.List<string>()
                        : global::Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(app.XamlRoot).Select(p => p.Child?.GetType().Name ?? "empty").ToList();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["popups"] = new Newtonsoft.Json.Linq.JArray(open) };
                })));
            // The test host's title markers ("AUTOMATION TEST HOST ·" and the connected client's note) on (show: true)
            // or off in every window, for screenshots that are to look like the app.
            // The running E2E case and its purpose, shown in every window's title after the test host's marker (label
            // empty: none). Not tied to a window: a case sets it before it has one.
            methods.Add(new MethodDescriptor("test.app.caseLabel", null, "test.app.caseLabel/1", async (c, ct) =>
            {
                Core.ViewModels.UIViewModel.TestCaseLabel = c.Params.OptionalString("label");
                foreach (var window in Core.Services.AutomationWindows.Registry.Snapshot())
                    await Core.Services.AutomationWindows.Registry.OnWindowAsync(window.WindowId, app => { app.UIViewModel.RefreshTitle(); return true; });
                return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject();
            }));
            methods.Add(new MethodDescriptor("test.app.titleMarker", null, "test.app.titleMarker/1", (c, ct) =>
            {
                Core.ViewModels.UIViewModel.ShowTestHostMarker = c.Params.OptionalBoolean("show", true);
                return Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    foreach (var vm in Core.ViewModels.AppViewModel.GetInstances()) vm.UIViewModel.RefreshTitle();
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["title"] = app.UIViewModel.MainWindowTitle };
                });
            }));
            // The window's active document saved as the path, as File > Save as with that file picked (an untitled one's
            // first save too); the path back, or null when it was not saved.
            methods.Add(new MethodDescriptor("test.file.saveAs", null, "test.file.saveAs/1", async (c, ct) =>
            {
                var path = c.Params.RequiredString("path", allowEmpty: false);
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                    (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["path"] = await app.FileViewModel.SaveAsTo(path) });
            }));
            // The window's document exported to a file, as File > Export > <type> with that file picked in the save dialog
            // (html or pdf: the first export configuration of that type); answers once the export is asked for, not done.
            methods.Add(new MethodDescriptor("test.export.run", null, "test.export.run/1", async (c, ct) =>
            {
                var type = c.Params.OptionalEnum("type", "html", "html", "pdf");
                var path = c.Params.RequiredString("path", allowEmpty: false);
                return await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    var want = type == "pdf" ? Core.Enums.ExportType.PDF : Core.Enums.ExportType.HTML;
                    var exports = app.ServiceProvider.GetService(typeof(Core.Interfaces.IFileExport)) as Core.Interfaces.IFileExport
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, "no export service");
                    // A host just started seeds its configurations a moment after the window opens.
                    for (var i = 0; i < 50 && !exports.ExportConfigs.Any(x => x.Type == want); i++)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        if (i % 10 == 9) await exports.UpdateExportConfigs();
                    }
                    var config = exports.ExportConfigs.FirstOrDefault(x => x.Type == want)
                        ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, $"no {type} export configuration");
                    app.FileViewModel.ExportTo(config, path);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["config"] = config.Name };
                });
            }));
            // The notice an export leaves (Settings > Export > After export): the file it names, or null when none shows.
            methods.Add(new MethodDescriptor("test.export.notice", null, "test.export.notice/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                    (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["path"] = app.UIViewModel.ExportedPath, ["text"] = app.UIViewModel.ExportedNotice })));
            // The entries of a window's File > Export submenu, as shown.
            methods.Add(new MethodDescriptor("test.export.menu", null, "test.export.menu/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    Core.Controls.EditorControls.MenuBarItems.FileItem? file = null;
                    void Find(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (file != null || node == null) return;
                        if (node is Core.Controls.EditorControls.MenuBarItems.FileItem f) { file = f; return; }
                        for (var i = 0; i < global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                            Find(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    Find(app.XamlRoot?.Content as global::Windows.UI.Xaml.DependencyObject);
                    if (file == null) throw new AutomationException(AutomationErrorKind.editor_not_ready, "no File menu in this window");
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["entries"] = new Newtonsoft.Json.Linq.JArray(file.ExportMenu()) };
                })));
            // The colours the side pane marks what is chosen with: every selection indicator drawn in it (the bar under
            // Files/Outline, the pill of each outline and folder-tree row), by where it is, with its fill.
            // The open dialog's colours and the custom theme's, to tell whether the dialog wears the theme.
            methods.Add(new MethodDescriptor("test.dialog.colours", null, "test.dialog.colours/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    // The dialog showing now: loaded and visible. One left in the window's grid after it closed is
                    // counted, not read.
                    var all = (app.XamlRoot?.Content as global::Windows.UI.Xaml.Controls.Panel)?.Children.OfType<Core.Controls.AppContentDialog>().ToList()
                        ?? new System.Collections.Generic.List<Core.Controls.AppContentDialog>();
                    var dialog = all.LastOrDefault(d => d.IsLoaded && d.Visibility == global::Windows.UI.Xaml.Visibility.Visible);
                    string Colour(global::Windows.UI.Xaml.Media.Brush brush) => (brush as global::Windows.UI.Xaml.Media.SolidColorBrush)?.Color.ToString();
                    var theme = Core.Utilities.ThemeFiles.Find(app.SettingsViewModel?.CustomTheme);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject
                    {
                        ["open"] = dialog != null,
                        ["inGrid"] = all.Count,
                        ["background"] = Colour(dialog?.Background),
                        ["foreground"] = Colour(dialog?.Foreground),
                        ["border"] = Colour(dialog?.BorderBrush),
                        ["theme"] = app.SettingsViewModel?.CustomTheme,
                        ["themeSurface"] = Colour(Core.Utilities.ThemeFiles.Brush(theme?.Surface) ?? Core.Utilities.ThemeFiles.Brush(theme?.Background)),
                        ["themeForeground"] = Colour(Core.Utilities.ThemeFiles.Brush(theme?.Foreground) ?? Core.Utilities.ThemeFiles.Readable(theme?.Surface ?? theme?.Background)),
                    };
                })));
            // The colours a custom theme changes around the editor, as they are now: the line between the side pane and
            // the editor, the side pane, the menu bar and the status bar. A theme switched off has to put back what the
            // built-in theme draws (TH06).
            methods.Add(new MethodDescriptor("test.theme.chrome", null, "test.theme.chrome/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    string Colour(global::Windows.UI.Xaml.Media.Brush brush) => brush switch
                    {
                        null => "none",
                        global::Windows.UI.Xaml.Media.SolidColorBrush solid => solid.Color.ToString() + (solid.Opacity < 1 ? $"@{solid.Opacity:0.##}" : ""),
                        _ => brush.GetType().Name,
                    };
                    var found = new Newtonsoft.Json.Linq.JObject();
                    void Walk(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (node is global::Windows.UI.Xaml.FrameworkElement fe)
                        {
                            if (fe.Name == "SplitterLine" && fe is global::Windows.UI.Xaml.Controls.Border line) found["splitter"] = Colour(line.Background);
                            if (fe is Core.Controls.StatusBar status && status.FindName("RootGrid") is global::Windows.UI.Xaml.Controls.Panel statusGrid) found["statusBar"] = Colour(statusGrid.Background);
                            if (fe is Core.Controls.MenuBar menu && menu.FindName("RootGrid") is global::Windows.UI.Xaml.Controls.Panel menuGrid) found["menuBar"] = Colour(menuGrid.Background);
                            if (fe.Name == "LeftPane" && fe is global::Windows.UI.Xaml.Controls.Control pane) { found["sidePane"] = Colour(pane.Background); found["sidePaneText"] = Colour(pane.Foreground); }
                        }
                        for (var i = 0; i < global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++) Walk(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    if (app.XamlRoot?.Content is global::Windows.UI.Xaml.DependencyObject root) Walk(root);
                    return (Newtonsoft.Json.Linq.JToken?)found;
                })));
            methods.Add(new MethodDescriptor("test.pane.accent", null, "test.pane.accent/1", (c, ct) =>
                Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), app =>
                {
                    var found = new Newtonsoft.Json.Linq.JArray();
                    string Where(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        for (; node != null; node = global::Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
                        {
                            if (node is Core.Controls.SidePanelControls.Pages.TocPage) return "outline";
                            if (node is Core.Controls.SidePanelControls.Pages.FolderPage) return "folder";
                            if (node is Microsoft.UI.Xaml.Controls.NavigationViewItem) return "tabs";
                        }
                        return "other";
                    }
                    void Walk(global::Windows.UI.Xaml.DependencyObject node)
                    {
                        if (node is global::Windows.UI.Xaml.Shapes.Shape shape && shape.Name.Contains("SelectionIndicator"))
                            found.Add(new Newtonsoft.Json.Linq.JObject { ["where"] = Where(node), ["name"] = shape.Name, ["fill"] = (shape.Fill as global::Windows.UI.Xaml.Media.SolidColorBrush)?.Color.ToString() });
                        for (var i = 0; i < global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                            Walk(global::Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                    }
                    Walk(app.XamlRoot?.Content as global::Windows.UI.Xaml.DependencyObject);
                    return (Newtonsoft.Json.Linq.JToken?)new Newtonsoft.Json.Linq.JObject { ["indicators"] = found };
                })));
            // File > Upload local images on the window's active document, without its dialogs; returns the result.
            methods.Add(new MethodDescriptor("test.images.uploadAll", null, "test.images.uploadAll/1", async (c, ct) =>
                await await Core.Services.AutomationWindows.Registry.OnWindowAsync(c.Params.RequiredString("windowId"), async app =>
                {
                    var result = await new Core.Services.ImageBatchUpload(app).RunAsync(null, ct);
                    return (Newtonsoft.Json.Linq.JToken?)Newtonsoft.Json.Linq.JObject.FromObject(result);
                })));
        }
#else
        public static IEditBarriers Barriers => null;

        public static void AddMethods(MethodTable methods) { }
#endif

    }
}
