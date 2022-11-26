using System;
using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;
using Typedown.Universal.Utilities;
using Typedown.Utilities;
using Typedown.Universal.Enums;
using Windows.UI.Xaml;
using Typedown.Universal.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.Foundation;
using PropertyChanged;
using Typedown.Universal;
using Typedown.Interfaces;

namespace Typedown.Windows
{
    public class AppWindow : FrameWindow
    {
        public static DependencyProperty ContentProperty { get; } = DependencyProperty.Register(nameof(Content), typeof(object), typeof(AppWindow), new(null, OnPropertyChanged));
        public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public static DependencyProperty DataContextProperty { get; } = DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(AppWindow), new(null, OnPropertyChanged));
        public object DataContext { get => GetValue(DataContextProperty); set => SetValue(DataContextProperty, value); }

        public static DependencyProperty ThemeProperty { get; } = DependencyProperty.Register(nameof(Theme), typeof(AppTheme), typeof(AppWindow), new(AppTheme.Default, OnPropertyChanged));
        public AppTheme Theme { get => (AppTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

        public static DependencyProperty CaptionHeightProperty { get; } = DependencyProperty.Register(nameof(CaptionHeight), typeof(double), typeof(AppWindow), new(32d, OnPropertyChanged));
        public double CaptionHeight { get => (double)GetValue(CaptionHeightProperty); set => SetValue(CaptionHeightProperty, value); }

        public static DependencyProperty IsMicaEnableProperty { get; } = DependencyProperty.Register(nameof(IsMicaEnable), typeof(bool), typeof(AppWindow), new(true, OnPropertyChanged));
        public bool IsMicaEnable { get => (bool)GetValue(IsMicaEnableProperty); set => SetValue(IsMicaEnableProperty, value); }

        public IntPtr XamlSourceHandle { get; private set; }

        private readonly WindowRootLayout rootLayout = new();

        private readonly ContentPresenter rootContent = new();

        private readonly DesktopWindowXamlSource xamlSource = new();

        private readonly UISettings uiSettings = new();

        public AppWindow()
        {
            rootLayout.Window = this;
            rootLayout.Children.Add(rootContent);
            xamlSource.Content = rootLayout;
            uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }

        protected override void OnCreated(EventArgs args)
        {
            base.OnCreated(args);

            var desktopWindowXamlSourceNative = xamlSource as IDesktopWindowXamlSourceNative;
            desktopWindowXamlSourceNative.AttachToWindow(Handle);
            CoreWindowHelper.DetachCoreWindow();
            XamlSourceHandle = desktopWindowXamlSourceNative.WindowHandle;

            PInvoke.DwmExtendFrameIntoClientArea(Handle, new PInvoke.MARGINS() { cyTopHeight = Config.IsMicaSupported ? -1 : 0 });
            PInvoke.SetWindowPos(Handle, 0, 0, 0, 0, 0, PInvoke.SetWindowPosFlags.SWP_FRAMECHANGED | PInvoke.SetWindowPosFlags.SWP_NOMOVE | PInvoke.SetWindowPosFlags.SWP_NOSIZE);

            UpdateClientPos();
            UpdateSystemBackdrop();
        }

        private void UpdateClientPos()
        {
            if (Handle == IntPtr.Zero || PInvoke.IsIconic(Handle))
                return;
            PInvoke.GetClientRect(Handle, out var rect);
            var rawBorderThiness = (int)(BorderThiness * ScalingFactor);
            var rawTopInvisibleHeight = State == WindowState.Maximized ? rawBorderThiness : 0;
            PInvoke.SetWindowPos(XamlSourceHandle, 0, 0, rawTopInvisibleHeight, rect.right, rect.bottom - rawTopInvisibleHeight, PInvoke.SetWindowPosFlags.SWP_SHOWWINDOW | PInvoke.SetWindowPosFlags.SWP_NOZORDER);
        }

        [SuppressPropertyChangedWarnings]
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as AppWindow;
            if (e.Property == ContentProperty)
            {
                target.rootContent.Content = e.NewValue as UIElement;
            }
            else if (e.Property == DataContextProperty)
            {
                target.rootLayout.DataContext = e.NewValue;
            }
            else if (e.Property == IsMicaEnableProperty || e.Property == ThemeProperty)
            {
                target.UpdateSystemBackdrop();
            }
        }

        public static AppWindow GetWindow(UIElement element)
        {
            if (element.XamlRoot.Content is WindowRootLayout rootLayout)
                return rootLayout.Window;
            return null;
        }

        private async void OnColorValuesChanged(UISettings sender, object args)
        {
            await Dispatcher.RunIdleAsync(_ => UpdateSystemBackdrop());
        }

        private void UpdateSystemBackdrop()
        {
            if (Handle == IntPtr.Zero)
                return;
            var isDarkMode = Theme switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => !Utilities.Common.GetUseLightTheme()
            };
            rootLayout.RequestedTheme = isDarkMode ? ElementTheme.Dark : ElementTheme.Light;
            this.SetDarkMode(isDarkMode);
            if (Config.IsMicaSupported)
            {
                this.SetMicaBackdrop(IsMicaEnable);
                this.SetCaptionColor(IsMicaEnable ? 0xffffffffu : isDarkMode ? 0x00202020u : 0x00f3f3f3u);
            }
            else
            {
                rootLayout.Background = new SolidColorBrush(isDarkMode ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20) : Color.FromArgb(0xFF, 0xf3, 0xf3, 0xf3));
            }
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnStateChanged(EventArgs args)
        {
            base.OnStateChanged(args);
            UpdateClientPos();
        }

        public void OpenSystemMenu(Point screenPos)
        {
            var hMenu = PInvoke.GetSystemMenu(Handle, false);
            var toEnable = (bool b) => b ? 0u : 2u; // MF_ENABLED:0, MF_DISABLED:2
            PInvoke.EnableMenuItem(hMenu, 0xF030, toEnable(State == WindowState.Normal)); // SC_MAXIMIZE
            PInvoke.EnableMenuItem(hMenu, 0xF120, toEnable(State == WindowState.Maximized)); // SC_RESTORE
            PInvoke.EnableMenuItem(hMenu, 0xF010, toEnable(State == WindowState.Normal)); // SC_MOVE
            PInvoke.EnableMenuItem(hMenu, 0xF000, toEnable(State == WindowState.Normal));// SC_SIZE
            PInvoke.EnableMenuItem(hMenu, 0xF020, toEnable(true)); // SC_MINIMIZE
            PInvoke.SetMenuDefaultItem(hMenu, State == WindowState.Maximized ? 0u : 4u, 1);
            var retvalue = PInvoke.TrackPopupMenu(hMenu, 0x0100, (int)screenPos.X, (int)screenPos.Y, 0, Handle, IntPtr.Zero);
            PInvoke.PostMessage(Handle, (uint)PInvoke.WindowMessage.WM_SYSCOMMAND, retvalue, IntPtr.Zero);
        }

        protected virtual PInvoke.HitTestFlags HitTest(Point pointerPos)
        {
            if (State == WindowState.Normal)
            {
                var isTop = pointerPos.Y < BorderThiness;
                var isBottom = pointerPos.Y > Height - BorderThiness;
                var isLeft = pointerPos.X < BorderThiness;
                var isRight = pointerPos.X > Width - 2 * BorderThiness;
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
            var topInvisibleHeight = State == WindowState.Maximized ? BorderThiness : 0;
            if (pointerPos.Y < CaptionHeight + topInvisibleHeight)
                return PInvoke.HitTestFlags.CAPTION;
            return PInvoke.HitTestFlags.CLIENT;
        }

        protected override IntPtr WndProc(nint hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCCALCSIZE:
                    if (wParam != IntPtr.Zero)
                    {
                        var p = (PInvoke.NCCALCSIZE_PARAMS)Marshal.PtrToStructure(lParam, typeof(PInvoke.NCCALCSIZE_PARAMS));
                        p.rcNewWindow.left += (int)(BorderThiness * ScalingFactor);
                        p.rcNewWindow.right -= (int)(BorderThiness * ScalingFactor);
                        p.rcNewWindow.bottom -= (int)(BorderThiness * ScalingFactor);
                        Marshal.StructureToPtr(p, lParam, true);
                        return IntPtr.Zero;
                    }
                    break;
                case PInvoke.WindowMessage.WM_NCHITTEST:
                    if (PInvoke.DwmDefWindowProc(hWnd, (int)msg, wParam, lParam, out var dwmHitTest))
                        return dwmHitTest;
                    var point = Utilities.Common.MakePoint(lParam);
                    PInvoke.GetWindowRect(hWnd, out var rect);
                    point.X = (point.X - rect.left) / ScalingFactor;
                    point.Y = (point.Y - rect.top) / ScalingFactor;
                    return (IntPtr)HitTest(point);
                case PInvoke.WindowMessage.WM_NCRBUTTONUP:
                    if (wParam.ToInt32() == (int)PInvoke.HitTestFlags.CAPTION)
                    {
                        OpenSystemMenu(Utilities.Common.MakePoint(lParam));
                    }
                    break;
                case PInvoke.WindowMessage.WM_NCMOUSELEAVE:
                    if (PInvoke.DwmDefWindowProc(hWnd, (int)msg, wParam, lParam, out var dwmRet))
                    {
                        return dwmRet;
                    }
                    break;
                case PInvoke.WindowMessage.WM_WINDOWPOSCHANGED:
                    UpdateClientPos();
                    break;
            }
            return base.WndProc(hWnd, msg, wParam, lParam);
        }
    }
}
