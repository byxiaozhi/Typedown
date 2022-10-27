using System;
using System.Collections.Generic;
using System.Text;

namespace Typedown.Universal.Models
{
    public class WndEventArgs : EventArgs
    {
        public IntPtr Hwnd { get; }
        public int Msg { get; }
        public IntPtr WParam { get; }
        public IntPtr LParam { get; }
        public bool Handled { get; set; }
        public IntPtr Result { get; set; }

        public WndEventArgs(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            Hwnd = hwnd;
            Msg = msg;
            WParam = wParam;
            LParam = lParam;
        }
    }
}
