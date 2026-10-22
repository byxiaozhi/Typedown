using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Graphics;

namespace Typedown.WinUI
{
    // New since the fork: the window of the teleprompter and of the speaking clock (docs/speech-marks-design.md, step 2). It holds a
    // WebView2 that shows the page of the editor's bundle in its "teleprompter" or "clock" mode (Typedown.Editor/src/teleprompter);
    // the page does everything (the plan, the scrolling, the clock, the keys) and asks this window for two things only: the text
    // (a "Script" message, sent when the page says it is ready and again when the text changes) and the window's own doings (full
    // screen, close). A second screen is the real use of the teleprompter, so it opens there when there is one; the clock is a
    // small window that stays on top. Nothing here is a model or a network call: the text comes from the main window.
    internal sealed class TeleprompterWindow : Window
    {
        private readonly WebView2 view = new();
        private readonly bool clockOnly;
        private readonly Func<Task<object>> buildScript;
        private bool fullScreen;

        // The page's other messages (the rehearsal's result, a pace to adopt) go to the main window, which owns the files and the
        // document; the answer goes back with Post.
        public Func<string, JToken, Task> Message { get; set; }

        public TeleprompterWindow(bool clockOnly, string title, Func<Task<object>> buildScript)
        {
            this.clockOnly = clockOnly;
            this.buildScript = buildScript;
            Title = title;
            Content = new Grid { Children = { view } };
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
            try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico")); } catch { }
            // the keys of the page (Space, the arrows, a clicker's Page Up/Down) need the focus to be in it
            Activated += (s, e) => view.Focus(FocusState.Programmatic);
            Place();
            _ = InitializeAsync();
        }

        // The teleprompter on the second screen when there is one, else a window on the screen it is on; the clock small, on top.
        private void Place()
        {
            if (clockOnly)
            {
                AppWindow.Resize(new SizeInt32(760, 150));
                if (AppWindow.Presenter is OverlappedPresenter small)
                {
                    small.IsAlwaysOnTop = true;
                    small.IsMaximizable = false;
                }
                return;
            }
            var areas = DisplayArea.FindAll();
            var other = Enumerable.Range(0, areas.Count).Select(i => areas[i]).FirstOrDefault(a => !a.IsPrimary);
            if (other != null && WorkAreaOf(other) is RectInt32 work)
            {
                AppWindow.MoveAndResize(work);
            }
            else
            {
                AppWindow.Resize(new SizeInt32(1100, 720));
            }
        }

        // The work area (the screen without the taskbar) of a display in screen coordinates, from Win32, which gives it in the
        // coordinates MoveAndResize takes on any arrangement of screens (the main window does the same for its tab strip).
        private static RectInt32? WorkAreaOf(DisplayArea area)
        {
            try
            {
                var monitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(area.DisplayId);
                var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
                if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return null;
                return new RectInt32(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor, Work;
            public uint Flags;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        private async Task InitializeAsync()
        {
            try
            {
                await view.EnsureCoreWebView2Async();
                var statics = Path.Combine(AppContext.BaseDirectory, "Resources", "Statics");
                view.CoreWebView2.SetVirtualHostNameToFolderMapping("typedown.editor.local", statics, CoreWebView2HostResourceAccessKind.Allow);
                // a page that only shows a talk: no browser keys, no context menu, no status bar
                view.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                view.CoreWebView2.Settings.IsStatusBarEnabled = false;
                view.CoreWebView2.WebMessageReceived += OnMessage;
                view.Source = new Uri("https://typedown.editor.local/index.html#" + (clockOnly ? "clock" : "teleprompter"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teleprompter: the page did not load: {ex.Message}");
            }
        }

        private async void OnMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var message = JObject.Parse(args.TryGetWebMessageAsString());
                switch (message["name"]?.ToString())
                {
                    case "TeleprompterReady":
                        await PushAsync();
                        break;
                    case "Fullscreen":
                        ToggleFullScreen();
                        break;
                    case "Escape":
                        if (fullScreen) ToggleFullScreen();
                        else Close();
                        break;
                    default:
                        if (Message != null) await Message(message["name"]?.ToString() ?? "", message["args"]);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teleprompter: a message was not understood: {ex.Message}");
            }
        }

        private void ToggleFullScreen()
        {
            fullScreen = !fullScreen;
            AppWindow.SetPresenter(fullScreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
        }

        public void Post(string name, object args)
        {
            try
            {
                view.CoreWebView2?.PostWebMessageAsString(JsonConvert.SerializeObject(new { name, args }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teleprompter: a message was not sent: {ex.Message}");
            }
        }

        // The text (and the words of the page in the language of the interface) goes to the page.
        public async Task PushAsync()
        {
            try
            {
                if (view.CoreWebView2 == null) return;
                var payload = await buildScript();
                view.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(new { name = "Script", args = payload }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teleprompter: the text was not sent: {ex.Message}");
            }
        }
    }
}
