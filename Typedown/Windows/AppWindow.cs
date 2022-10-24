using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Typedown.Windows
{
    public class AppWindow : Window
    {
        public nint Handle => new WindowInteropHelper(this).Handle;

        public double WindowScale => PInvoke.GetDpiForWindow(new(Handle)) / 96.0;

        public int RawBorderWidth => PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXFRAME) + PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXPADDEDBORDER);

        public double BorderWidth => RawBorderWidth / WindowScale;

        public double CaptionHeight { get; set; } = 32;

        public AppWindow()
        {
            Style = Application.Current.Resources["DefaultAppWindowStyle"] as Style;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var style = PInvoke.GetWindowLong(new(Handle), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            PInvoke.SetWindowLong(new(Handle), WINDOW_LONG_PTR_INDEX.GWL_STYLE, style & ~(int)WINDOW_STYLE.WS_CLIPCHILDREN);
            PInvoke.DwmExtendFrameIntoClientArea(new(Handle), new MARGINS());
            PInvoke.SetWindowPos(new(Handle), HWND.Null, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
            HwndSource.FromHwnd(Handle).AddHook(WndProc);
            UpdateRootElement();
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

        private void UpdateRootElement()
        {
            if (GetTemplateChild("PART_RootElement") is FrameworkElement ele)
                ele.Margin = new(0, WindowState == WindowState.Maximized ? BorderWidth : 1, 0, 0);
        }

        private static Point MakePoint(IntPtr p) => new(p.ToInt32() & 0xFFFF, p.ToInt32() >> 16);
    }
}
