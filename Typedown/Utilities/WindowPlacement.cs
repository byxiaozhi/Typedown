using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Interop;
using System.Windows;
using Windows.System;
using Typedown.Windows;

namespace Typedown.Utilities
{
    public static class WindowPlacement
    {
        public static PInvoke.WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
        {
            PInvoke.WINDOWPLACEMENT placement = new();
            PInvoke.GetWindowPlacement(hwnd, ref placement);
            var scale = PInvoke.GetDpiForWindow(hwnd) / 96.0;
            placement.rcNormalPosition.top = (int)(placement.rcNormalPosition.top / scale);
            placement.rcNormalPosition.left = (int)(placement.rcNormalPosition.left / scale);
            placement.rcNormalPosition.right = (int)(placement.rcNormalPosition.right / scale);
            placement.rcNormalPosition.bottom = (int)(placement.rcNormalPosition.bottom / scale);
            placement.ptMaxPosition.X = (int)(placement.ptMaxPosition.X / scale);
            placement.ptMaxPosition.Y = (int)(placement.ptMaxPosition.Y / scale);
            return placement;
        }

        public static PInvoke.WINDOWPLACEMENT GetWindowPlacement(AppWindow window)
        {
            return GetWindowPlacement(window.Handle);
        }

        public static void RestoreWindowPlacement(Window window, PInvoke.WINDOWPLACEMENT placement)
        {
            var position = placement.rcNormalPosition;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = position.left;
            window.Top = position.top;
            window.Width = (position.right - position.left);
            window.Height = (position.bottom - position.top);
            window.WindowState = placement.showCmd == PInvoke.ShowWindowCommand.Maximize ? WindowState.Maximized : WindowState.Normal;
        }
    }
}
