using System;
using System.Reactive.Disposables;
using Typedown.Core.Controls.EditorControls.MenuBarItems;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.XamlUI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        /// <summary>
        /// Paints this bar from a custom theme. Null puts the colours of the built-in theme back; the inner
        /// grid carries the visible background, so setting the control's own is not enough.
        /// </summary>
        /// <summary>
        /// Turns the compact menu bar's window-drag areas on or off. Each drag area is a separate native window laid
        /// over its element, and it stays where it was when the element is merely collapsed: in full screen the menu
        /// bar is hidden, the tabs move up into its place, and the drag window left on top of them took every click
        /// as a title-bar drag - an invisible thing covering the tabs. Full screen has no window to drag, so the areas
        /// are detached (their native windows destroyed) while it lasts.
        /// </summary>
        public void SetDragEnabled(bool enabled)
        {
            foreach (var bar in new Windows.UI.Xaml.FrameworkElement[] { LeftDragBar, RightDragBar })
                if (bar != null && Typedown.XamlUI.XamlWindow.GetDrag(bar) != enabled)
                {
                    Typedown.XamlUI.XamlWindow.SetDrag(bar, enabled);
                    if (enabled) EnsureDragWindow(bar);
                }
        }

        /// <summary>
        /// Turned on again (out of full screen), an area got no native window: XamlUI's DragBar makes it when the
        /// element loads, and an element that stayed loaded never does - the window could not be dragged until the main
        /// page was built anew (the settings visited). Then it is made here, as DragBar makes it on loading.
        /// </summary>
        private static void EnsureDragWindow(Windows.UI.Xaml.FrameworkElement bar)
        {
            try
            {
                var dragBar = Typedown.XamlUI.DragBar.AttachToFrameworkElement(bar);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                if (dragBar.GetType().GetField("_dragBarWindow", flags)?.GetValue(dragBar) != null) return;
                dragBar.GetType().GetMethod("CreateDragBarWindow", flags)?.Invoke(dragBar, new object[] { bar });
                if (dragBar.GetType().GetField("_dragBarWindow", flags)?.GetValue(dragBar) == null)
                    Utilities.Log.Debug($"drag: no window for {bar.Name} after turning it on again");
            }
            catch (Exception ex)
            {
                Utilities.Log.Debug($"drag: could not make the window for {bar.Name}: {ex.Message}");
            }
        }

        public void ApplyThemeBrushes(Windows.UI.Xaml.Media.Brush background, Windows.UI.Xaml.Media.Brush foreground)
        {
            if (RootGrid != null)
            {
                if (background != null) RootGrid.Background = background;
                else RootGrid.ClearValue(Windows.UI.Xaml.Controls.Panel.BackgroundProperty);
            }
            if (foreground != null) Foreground = foreground;
            else ClearValue(ForegroundProperty);
        }

        /// <summary>
        /// The line under the menu row. It separates the menu from the tabs when both have the same background (the
        /// built-in themes); when a custom theme paints the menu row with its own panel colour, the change of colour
        /// already does that and the line only makes the header heavier.
        /// </summary>
        public void SetDivider(bool visible)
        {
            if (RootGrid != null) RootGrid.BorderThickness = visible ? new Thickness(0, 0, 0, 1) : new Thickness(0);
        }

        public AppViewModel ViewModel => DataContext as AppViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            if (TitleGrid != null)
            {
                if (ActualWidth / 2 > MenuBarControl.ActualWidth + TitleTextBlock.ActualWidth / 2 + 16)
                {
                    TitleGrid.Margin = new(0);
                    Grid.SetColumn(TitleGrid, 0);
                    Grid.SetColumnSpan(TitleGrid, 3);
                }
                else
                {
                    TitleGrid.Margin = new(0, 0, 46 * 3, 0);
                    Grid.SetColumn(TitleGrid, 1);
                    Grid.SetColumnSpan(TitleGrid, 2);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.AppCompactMode)
            {
                var uiViewModel = ViewModel.UIViewModel;
                var oldCaptionHeight = uiViewModel.CaptionHeight;
                uiViewModel.CaptionHeight = 40;
                disposables.Add(Disposable.Create(() => uiViewModel.CaptionHeight = oldCaptionHeight));
            }
            // A language changed while this window shows its document (through the automation API, or another window's
            // settings) passes no page that would build the menus again, and they kept the old language while the side
            // pane and the status bar followed. Their titles and items are read from {u:LocaleString} when they are
            // created, so the menus - and only they - are created again. The main page used to be built again instead,
            // which reloaded the editor page: a read or a write right then could fail, and two pages talking to the host
            // at once could end the process.
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.Language)).Subscribe(_ =>
                _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, RebuildMenus)));
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.SourceCode)).Subscribe(_ => ApplyModeToMenus()));
        }

        private void RebuildMenus()
        {
            try
            {
                var items = MenuBarControl.Items;
                items.Clear();
                items.Add(new FileItem());
                items.Add(new EditItem());
                items.Add(new ParagraphItem());
                items.Add(new FormatItem());
                items.Add(new ViewItem());
                ApplyModeToMenus();
            }
            catch (Exception ex)
            {
                Log.Debug($"menus after a language change: {ex.Message}");
            }
        }

        // Paragraph and Format have nothing to do in source mode (the markup binds this for the menus it creates).
        private void ApplyModeToMenus()
        {
            if (Settings == null) return;
            var visibility = IsCollapsed(Settings.SourceCode);
            foreach (var item in MenuBarControl.Items)
                if (item is ParagraphItem || item is FormatItem) item.Visibility = visibility;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Dispose();
            Bindings?.StopTracking();
        }

        private Visibility IsCollapsed(bool boolean) => boolean ? Visibility.Collapsed : Visibility.Visible;

        private DateTime prevLeftButtonPressedTime = DateTime.Now;

        private void OnMenuBarPointerEvent(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Settings.AppCompactMode && e.OriginalSource is Grid && XamlWindow.GetWindow(this) is XamlWindow window)
            {
                _ = Dispatcher.RunIdleAsync(() =>
                {
                    PInvoke.GetCursorPos(out var point);
                    var packedPoint = (point.Y << 16) + point.X;
                    var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
                    if (kind == PointerUpdateKind.LeftButtonPressed)
                    {
                        if ((DateTime.Now - prevLeftButtonPressedTime).TotalMilliseconds < PInvoke.GetDoubleClickTime())
                            window.PostMessage((uint)PInvoke.WindowMessage.WM_NCLBUTTONDBLCLK, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                        else
                            window.PostMessage((uint)PInvoke.WindowMessage.WM_NCLBUTTONDOWN, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                        prevLeftButtonPressedTime = DateTime.Now;
                    }
                    if (kind == PointerUpdateKind.RightButtonReleased)
                    {
                        window.PostMessage((uint)PInvoke.WindowMessage.WM_NCRBUTTONUP, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                    }
                });
            }
        }
    }
}
