using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Typedown.Windows
{
    public class AppWindow : Window
    {
        public static DependencyProperty ThemeProperty = DependencyProperty.Register("Theme", typeof(string), typeof(AppWindow));

        public string Theme { get => (string)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

        public nint Handle { get; private set; }

        public double WindowScale => PInvoke.GetDpiForWindow(new(Handle)) / 96.0;

        public int RawBorderWidth => PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXFRAME) + PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXPADDEDBORDER);

        public double BorderWidth => RawBorderWidth / WindowScale;

        public double CaptionHeight { get; set; } = 32;

        public static bool IsMicaSupported { get; } = Environment.OSVersion.Version.Build >= 22000;

        public UISettings uiSettings = new();

        public AppWindow()
        {
            Style = Application.Current.Resources["DefaultAppWindowStyle"] as Style;
            uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Handle = new WindowInteropHelper(this).Handle;
            var style = PInvoke.GetWindowLong(new(Handle), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            PInvoke.SetWindowLong(new(Handle), WINDOW_LONG_PTR_INDEX.GWL_STYLE, style & ~(int)WINDOW_STYLE.WS_CLIPCHILDREN);
            PInvoke.DwmExtendFrameIntoClientArea(new(Handle), new MARGINS() { cyTopHeight = IsMicaSupported ? -1 : 0 });
            PInvoke.SetWindowPos(new(Handle), HWND.Null, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
            HwndSource.FromHwnd(Handle).AddHook(WndProc);
            UpdateRootElement();
            UpdateSystemBackdrop();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            UpdateRootElement();
        }

        public unsafe void OpenSystemMenu(Point screenPos)
        {
            var hMenu = PInvoke.GetSystemMenu(new(Handle), false);
            var toEnable = (bool b) => b ? MENU_ITEM_FLAGS.MF_ENABLED : MENU_ITEM_FLAGS.MF_DISABLED;
            var canResize = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
            PInvoke.EnableMenuItem(hMenu, PInvoke.SC_MAXIMIZE, toEnable(WindowState == WindowState.Normal && canResize));
            PInvoke.EnableMenuItem(hMenu, PInvoke.SC_RESTORE, toEnable(WindowState == WindowState.Maximized && canResize));
            PInvoke.EnableMenuItem(hMenu, PInvoke.SC_MOVE, toEnable(WindowState == WindowState.Normal));
            PInvoke.EnableMenuItem(hMenu, PInvoke.SC_SIZE, toEnable(WindowState == WindowState.Normal && canResize));
            PInvoke.EnableMenuItem(hMenu, PInvoke.SC_MINIMIZE, toEnable(ResizeMode != ResizeMode.NoResize));
            var retvalue = PInvoke.TrackPopupMenu(hMenu, TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD, (int)screenPos.X, (int)screenPos.Y, 0, new(Handle), (RECT*)IntPtr.Zero);
            PInvoke.PostMessage(new HWND(Handle), PInvoke.WM_SYSCOMMAND, new WPARAM((nuint)retvalue.Value), IntPtr.Zero);
        }

        private uint HitTest(Point pointerPos)
        {
            if (WindowState == WindowState.Normal && (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip))
            {
                var isTop = pointerPos.Y < BorderWidth;
                var isBottom = pointerPos.Y > ActualHeight - BorderWidth;
                var isLeft = pointerPos.X < 0;
                var isRight = pointerPos.X > ActualWidth - 2 * BorderWidth;
                if (isTop)
                {
                    if (isLeft) return PInvoke.HTTOPLEFT;
                    else if (isRight) return PInvoke.HTTOPRIGHT;
                    else return PInvoke.HTTOP;
                }
                else if (isBottom)
                {
                    if (isLeft) return PInvoke.HTBOTTOMLEFT;
                    else if (isRight) return PInvoke.HTBOTTOMRIGHT;
                    else return PInvoke.HTBOTTOM;
                }
                else if (isLeft) return PInvoke.HTLEFT;
                else if (isRight) return PInvoke.HTRIGHT;
            }
            if ((WindowState == WindowState.Maximized && pointerPos.Y < CaptionHeight + BorderWidth) || pointerPos.Y < CaptionHeight)
                return PInvoke.HTCAPTION;
            return PInvoke.HTCLIENT;
        }

        private unsafe IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((uint)msg)
            {
                case PInvoke.WM_NCCALCSIZE:
                    if (wParam != IntPtr.Zero)
                    {
                        handled = true;
                        var p = (NCCALCSIZE_PARAMS)Marshal.PtrToStructure(lParam, typeof(NCCALCSIZE_PARAMS));
                        p.rgrc[0].left += RawBorderWidth;
                        p.rgrc[0].right -= RawBorderWidth;
                        p.rgrc[0].bottom -= RawBorderWidth;
                        Marshal.StructureToPtr(p, lParam, true);
                    }
                    break;
                case PInvoke.WM_NCHITTEST:
                    handled = true;
                    if (PInvoke.DwmDefWindowProc(new(hWnd), (uint)msg, (uint)wParam, lParam, out var dwmHitTest))
                        return dwmHitTest;
                    return new(HitTest(PointFromScreen(MakePoint(lParam))));
                case PInvoke.WM_NCRBUTTONUP:
                    if (wParam.ToInt32() == PInvoke.HTCAPTION)
                    {
                        handled = true;
                        OpenSystemMenu(MakePoint(lParam));
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            switch (e.Property.Name)
            {
                case nameof(Theme):
                    UpdateSystemBackdrop();
                    break;
            }
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            Dispatcher.BeginInvoke(UpdateSystemBackdrop);
        }

        private void UpdateRootElement()
        {
            if (GetTemplateChild("PART_RootElement") is FrameworkElement ele)
                ele.Margin = new(0, WindowState == WindowState.Maximized ? BorderWidth : 1, 0, 0);
        }

        private unsafe void UpdateSystemBackdrop()
        {
            var compositionTarget = HwndSource.FromHwnd(Handle).CompositionTarget;
            var theme = Theme switch
            {
                "Light" => global::Windows.UI.Xaml.ApplicationTheme.Light,
                "Dark" => global::Windows.UI.Xaml.ApplicationTheme.Dark,
                _ => global::Windows.UI.Xaml.Application.Current.RequestedTheme
            };
            var isDarkMode = theme == global::Windows.UI.Xaml.ApplicationTheme.Dark;
            if (IsMicaSupported)
            {
                compositionTarget.BackgroundColor = Colors.Transparent;
                if (Environment.OSVersion.Version.Build >= 22523)
                {
                    uint micaValue = 0x02;
                    PInvoke.DwmSetWindowAttribute(new(Handle), (DWMWINDOWATTRIBUTE)38, &micaValue, (uint)Marshal.SizeOf(typeof(uint)));
                }
                else
                {
                    uint trueValue = 0x01;
                    PInvoke.DwmSetWindowAttribute(new(Handle), (DWMWINDOWATTRIBUTE)1029, &trueValue, (uint)Marshal.SizeOf(typeof(uint)));
                }
                var darkModeValue = isDarkMode ? 1u : 0u;
                PInvoke.DwmSetWindowAttribute(new(Handle), DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &darkModeValue, (uint)Marshal.SizeOf(typeof(uint)));
            }
            else
            {
                compositionTarget.BackgroundColor = isDarkMode ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xf3, 0xf3, 0xf3);
            }
        }

        private static Point MakePoint(IntPtr p) => new(p.ToInt32() & 0xFFFF, p.ToInt32() >> 16);
    }
}
