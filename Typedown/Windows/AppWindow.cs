using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.UI.ViewManagement;
using Typedown.Utilities;
using Typedown.Controls;

namespace Typedown.Windows
{
    public class AppWindow : Window
    {
        public static DependencyProperty ThemeProperty = DependencyProperty.Register("Theme", typeof(string), typeof(AppWindow));
        public string Theme { get => (string)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

        public static DependencyProperty CaptionHeightProperty = DependencyProperty.Register("CaptionHeight", typeof(double), typeof(AppWindow), new(32d));
        public double CaptionHeight { get => (double)GetValue(CaptionHeightProperty); set => SetValue(CaptionHeightProperty, value); }

        public nint Handle { get; private set; }

        public double WindowScale => PInvoke.GetDpiForWindow(new(Handle)) / 96.0;

        protected DragBar DragBar => GetTemplateChild("PART_DragBar") as DragBar;

        private int RawBorderWidth => PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CXFRAME) + PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CXPADDEDBORDER);

        private double BorderWidth => RawBorderWidth / WindowScale;

        public UISettings uiSettings = new();

        public AppWindow()
        {
            Template = CreateTemplate();
            uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Handle = new WindowInteropHelper(this).Handle;
            var style = PInvoke.GetWindowLong(Handle, PInvoke.WindowLongFlags.GWL_STYLE);
            PInvoke.SetWindowLong(Handle, PInvoke.WindowLongFlags.GWL_STYLE, style & ~(int)PInvoke.WindowStyles.WS_CLIPCHILDREN);
            PInvoke.DwmExtendFrameIntoClientArea(Handle, new PInvoke.MARGINS() { cyTopHeight = Universal.Config.IsMicaEnable ? -1 : 0 });
            PInvoke.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, PInvoke.SetWindowPosFlags.SWP_FRAMECHANGED | PInvoke.SetWindowPosFlags.SWP_NOMOVE | PInvoke.SetWindowPosFlags.SWP_NOSIZE);
            HwndSource.FromHwnd(Handle).AddHook(WndProc);
            UpdateRootContainer();
            UpdateSystemBackdrop();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            UpdateRootContainer();
        }

        public void OpenSystemMenu(Point screenPos)
        {
            var hMenu = PInvoke.GetSystemMenu(Handle, false);
            var toEnable = (bool b) => b ? 0u : 2u; // MF_ENABLED:0, MF_DISABLED:2
            var canResize = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
            PInvoke.EnableMenuItem(hMenu, 0xF030, toEnable(WindowState == WindowState.Normal && canResize)); // SC_MAXIMIZE
            PInvoke.EnableMenuItem(hMenu, 0xF120, toEnable(WindowState == WindowState.Maximized && canResize)); // SC_RESTORE
            PInvoke.EnableMenuItem(hMenu, 0xF010, toEnable(WindowState == WindowState.Normal)); // SC_MOVE
            PInvoke.EnableMenuItem(hMenu, 0xF000, toEnable(WindowState == WindowState.Normal && canResize));// SC_SIZE
            PInvoke.EnableMenuItem(hMenu, 0xF020, toEnable(ResizeMode != ResizeMode.NoResize)); // SC_MINIMIZE
            var retvalue = PInvoke.TrackPopupMenu(hMenu, 0x0100, (int)screenPos.X, (int)screenPos.Y, 0, Handle, IntPtr.Zero);
            PInvoke.PostMessage(Handle, (uint)PInvoke.WindowMessage.WM_SYSCOMMAND, new(retvalue), IntPtr.Zero);
        }

        protected virtual PInvoke.HitTestFlags HitTest(Point pointerPos)
        {
            if (WindowState == WindowState.Normal && (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip))
            {
                var isTop = pointerPos.Y < BorderWidth;
                var isBottom = pointerPos.Y > ActualHeight - BorderWidth;
                var isLeft = pointerPos.X < 0;
                var isRight = pointerPos.X > ActualWidth - 2 * BorderWidth;
                if (isTop)
                {
                    if (isLeft) return PInvoke.HitTestFlags.TOPLEFT;
                    else if (isRight) return PInvoke.HitTestFlags.TOPRIGHT;
                    else return PInvoke.HitTestFlags.TOP;
                }
                else if (isBottom)
                {
                    if (isLeft) return PInvoke.HitTestFlags.BOTTOMLEFT;
                    else if (isRight) return PInvoke.HitTestFlags.BOTTOMRIGHT;
                    else return PInvoke.HitTestFlags.BOTTOM;
                }
                else if (isLeft) return PInvoke.HitTestFlags.LEFT;
                else if (isRight) return PInvoke.HitTestFlags.RIGHT;
            }
            if ((WindowState == WindowState.Maximized && pointerPos.Y < CaptionHeight + BorderWidth) || pointerPos.Y < CaptionHeight)
                return PInvoke.HitTestFlags.CAPTION;
            return PInvoke.HitTestFlags.CLIENT;
        }

        protected virtual IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCCALCSIZE:
                    if (wParam != IntPtr.Zero)
                    {
                        handled = true;
                        var p = (PInvoke.NCCALCSIZE_PARAMS)Marshal.PtrToStructure(lParam, typeof(PInvoke.NCCALCSIZE_PARAMS));
                        p.rcNewWindow.left += RawBorderWidth;
                        p.rcNewWindow.right -= RawBorderWidth;
                        p.rcNewWindow.bottom -= RawBorderWidth;
                        Marshal.StructureToPtr(p, lParam, true);
                    }
                    break;
                case PInvoke.WindowMessage.WM_NCHITTEST:
                    handled = true;
                    if (PInvoke.DwmDefWindowProc(hWnd, msg, wParam, lParam, out var dwmHitTest))
                        return dwmHitTest;
                    return (IntPtr)HitTest(PointFromScreen(Common.MakePoint(lParam)));
                case PInvoke.WindowMessage.WM_NCRBUTTONUP:
                    if (wParam.ToInt32() == (int)PInvoke.HitTestFlags.CAPTION)
                    {
                        handled = true;
                        OpenSystemMenu(Common.MakePoint(lParam));
                    }
                    break;
                case PInvoke.WindowMessage.WM_NCMOUSELEAVE:
                    if (PInvoke.DwmDefWindowProc(hWnd, msg, wParam, lParam, out var dwmRet))
                    {
                        handled = true;
                        return dwmRet;
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

        private void UpdateRootContainer()
        {
            if (GetTemplateChild("PART_RootContainer") is FrameworkElement ele)
                ele.Margin = new(0, WindowState == WindowState.Maximized ? BorderWidth : 1, 0, 0);
        }

        private void UpdateSystemBackdrop()
        {
            var compositionTarget = HwndSource.FromHwnd(Handle).CompositionTarget;
            var theme = Theme switch
            {
                "Light" => global::Windows.UI.Xaml.ApplicationTheme.Light,
                "Dark" => global::Windows.UI.Xaml.ApplicationTheme.Dark,
                _ => global::Windows.UI.Xaml.Application.Current.RequestedTheme
            };
            var isDarkMode = theme == global::Windows.UI.Xaml.ApplicationTheme.Dark;
            if (Universal.Config.IsMicaEnable)
            {
                compositionTarget.BackgroundColor = Colors.Transparent;
                if (Environment.OSVersion.Version.Build >= 22523)
                {
                    uint micaValue = 0x02;
                    PInvoke.DwmSetWindowAttribute(Handle, PInvoke.DwmWindowAttribute.DWMWA_SYSTEMBACKDROP_TYPE, ref micaValue, Marshal.SizeOf(typeof(uint)));
                }
                else
                {
                    uint trueValue = 0x01;
                    PInvoke.DwmSetWindowAttribute(Handle, PInvoke.DwmWindowAttribute.DWMWA_MICA_EFFECT, ref trueValue, Marshal.SizeOf(typeof(uint)));
                }
                var darkModeValue = isDarkMode ? 1u : 0u;
                PInvoke.DwmSetWindowAttribute(Handle, PInvoke.DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, Marshal.SizeOf(typeof(uint)));
            }
            else
            {
                compositionTarget.BackgroundColor = isDarkMode ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xf3, 0xf3, 0xf3);
            }
        }

        private static ControlTemplate CreateTemplate()
        {
            var template = new ControlTemplate(typeof(AppWindow));
            var container = new FrameworkElementFactory(typeof(Grid)) { Name = "PART_RootContainer" };
            var content = new FrameworkElementFactory(typeof(ContentPresenter)) { Name = "PART_Content" };
            var dragBar = new FrameworkElementFactory(typeof(DragBar)) { Name = "PART_DragBar" };
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentProperty));
            dragBar.SetValue(HeightProperty, new TemplateBindingExtension(CaptionHeightProperty));
            dragBar.SetValue(VerticalAlignmentProperty, VerticalAlignment.Top);
            container.AppendChild(content);
            container.AppendChild(dragBar);
            template.VisualTree = container;
            return template;
        }
    }
}
