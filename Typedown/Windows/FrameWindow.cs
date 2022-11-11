using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;

namespace Typedown.Windows
{
    public partial class FrameWindow : DependencyObject, INotifyPropertyChanged
    {
        private const string className = "FRAME";

        private static readonly Dictionary<nint, FrameWindow> windows = new();
        public static IEnumerable<FrameWindow> Windows => windows.Values;

        private static readonly PInvoke.WindowProc staticWndProc = new(StaticWndProc);

        static FrameWindow()
        {
            PInvoke.EnableMouseInPointer(true);
            RegisterClass();
        }

        private static bool RegisterClass()
        {
            var wndClass = new PInvoke.WNDCLASSEX();
            wndClass.cbSize = Marshal.SizeOf(wndClass);
            wndClass.style = PInvoke.ClassStyles.HorizontalRedraw | PInvoke.ClassStyles.VerticalRedraw;
            wndClass.lpfnWndProc = staticWndProc;
            wndClass.cbClsExtra = 0;
            wndClass.cbWndExtra = 0;
            wndClass.hInstance = Process.GetCurrentProcess().Handle;
            wndClass.hIcon = IntPtr.Zero;
            wndClass.hCursor = PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW);
            wndClass.hbrBackground = 0;
            wndClass.lpszMenuName = null;
            wndClass.lpszClassName = className;
            var result = PInvoke.RegisterClassEx(ref wndClass);
            return result != 0;
        }

        private static IntPtr StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (windows.TryGetValue(hWnd, out var val))
            {
                var result = val.WndProc(hWnd, msg, wParam, lParam);
                if (msg == (uint)PInvoke.WindowMessage.WM_DESTROY)
                    windows.Remove(hWnd);
                return result;
            }
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public static DependencyProperty LeftProperty { get; } = DependencyProperty.Register(nameof(Left), typeof(double), typeof(Window), new(100d, OnPropertyChanged));
        public double Left { get => (double)GetValue(LeftProperty); set => SetValue(LeftProperty, value); }

        public static DependencyProperty TopProperty { get; } = DependencyProperty.Register(nameof(Top), typeof(double), typeof(Window), new(100d, OnPropertyChanged));
        public double Top { get => (double)GetValue(TopProperty); set => SetValue(TopProperty, value); }

        public static DependencyProperty WidthProperty { get; } = DependencyProperty.Register(nameof(Width), typeof(double), typeof(Window), new(800d, OnPropertyChanged));
        public double Width { get => (double)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

        public static DependencyProperty HeightProperty { get; } = DependencyProperty.Register(nameof(Height), typeof(double), typeof(Window), new(600d, OnPropertyChanged));
        public double Height { get => (double)GetValue(HeightProperty); set => SetValue(HeightProperty, value); }

        public static DependencyProperty MinWidthProperty { get; } = DependencyProperty.Register(nameof(MinWidth), typeof(double), typeof(Window), new(0d, OnPropertyChanged));
        public double MinWidth { get => (double)GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }

        public static DependencyProperty MinHeightProperty { get; } = DependencyProperty.Register(nameof(MinHeight), typeof(double), typeof(Window), new(0d, OnPropertyChanged));
        public double MinHeight { get => (double)GetValue(MinHeightProperty); set => SetValue(MinHeightProperty, value); }

        public static DependencyProperty BorderThinessProperty { get; } = DependencyProperty.Register(nameof(BorderThiness), typeof(double), typeof(Window), new(default(double), OnPropertyChanged));
        public double BorderThiness { get => (double)GetValue(BorderThinessProperty); private set => SetValue(BorderThinessProperty, value); }

        public static DependencyProperty ScaleProperty { get; } = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(Window), new(default(double), OnPropertyChanged));
        public double Scale { get => (double)GetValue(ScaleProperty); private set => SetValue(ScaleProperty, value); }

        public static DependencyProperty IsActivedProperty { get; } = DependencyProperty.Register(nameof(IsActived), typeof(double), typeof(Window), new(default(bool), OnPropertyChanged));
        public bool IsActived { get => (bool)GetValue(IsActivedProperty); private set => SetValue(IsActivedProperty, value); }

        public static DependencyProperty StateProperty { get; } = DependencyProperty.Register(nameof(State), typeof(WindowState), typeof(Window), new(WindowState.Normal, OnPropertyChanged));
        public WindowState State { get => (WindowState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

        public static DependencyProperty StartupLocationProperty { get; } = DependencyProperty.Register(nameof(StartupLocation), typeof(WindowStartupLocation), typeof(Window), new(WindowStartupLocation.Manual, OnPropertyChanged));
        public WindowStartupLocation StartupLocation { get => (WindowStartupLocation)GetValue(StartupLocationProperty); set => SetValue(StartupLocationProperty, value); }

        public static DependencyProperty TitleProperty { get; } = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Window), new("Typedown", OnPropertyChanged));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public nint Handle { get; private set; }

        public event EventHandler<EventArgs> Created;

        public event EventHandler<CancelEventArgs> Closing;

        public event EventHandler<EventArgs> Closed;

        public event EventHandler<EventArgs> LocationChanged;

        public event EventHandler<EventArgs> SizeChanged;

        public event EventHandler<EventArgs> StateChanged;

        public event EventHandler<EventArgs> ScaleChanged;

        public event EventHandler<EventArgs> IsActivedChanged;

        private bool isInternalChange = false;

        protected virtual void OnCreated(EventArgs args)
        {
            Created?.Invoke(this, args);
        }

        protected virtual void OnClosing(CancelEventArgs args)
        {
            Closing?.Invoke(this, args);
        }

        protected virtual void OnClosed(EventArgs args)
        {
            Closed?.Invoke(this, args);
        }

        protected virtual void OnLocationChanged(EventArgs args)
        {
            LocationChanged?.Invoke(this, args);
        }

        protected virtual void OnSizeChanged(EventArgs args)
        {
            SizeChanged?.Invoke(this, args);
        }

        protected virtual void OnStateChanged(EventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }

        protected virtual void OnScaleChanged(EventArgs args)
        {
            ScaleChanged?.Invoke(this, args);
        }

        protected virtual void OnIsActivedChanged(EventArgs args)
        {
            IsActivedChanged?.Invoke(this, args);
        }

        private void CreateWindow()
        {
            var style = PInvoke.WindowStyles.WS_OVERLAPPEDWINDOW;
            if (State == WindowState.Maximized)
                style |= PInvoke.WindowStyles.WS_MAXIMIZE;
            var exStyle = PInvoke.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP;
            Handle = PInvoke.CreateWindowEx(
                exStyle, className, Title, style,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                Process.GetCurrentProcess().Handle,
                IntPtr.Zero);
            UpdateScaleProperty();
            UpdateBorderThinessProperty();
            SetStartupPos();
            windows.Add(Handle, this);
            OnCreated(EventArgs.Empty);
        }

        public void EnsureCreate()
        {
            if (Handle == default)
                CreateWindow();
        }

        public void Show()
        {
            EnsureCreate();
            PInvoke.ShowWindow(Handle, State switch
            {
                WindowState.Maximized => PInvoke.ShowWindowCommand.Maximize,
                _ => PInvoke.ShowWindowCommand.Normal
            });
        }

        public void Close()
        {
            var cancelArgs = new CancelEventArgs();
            OnClosing(cancelArgs);
            if (cancelArgs.Cancel)
                return;
            PInvoke.DestroyWindow(Handle);
            Handle = default;
        }

        protected virtual IntPtr WndProc(nint hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_CLOSE:
                    Close();
                    return IntPtr.Zero;
                case PInvoke.WindowMessage.WM_DESTROY:
                    OnClosed(EventArgs.Empty);
                    break;
                case PInvoke.WindowMessage.WM_WINDOWPOSCHANGED:
                    UpdatePosProperty();
                    break;
                case PInvoke.WindowMessage.WM_SIZE:
                    UpdateStateProperty();
                    break;
                case PInvoke.WindowMessage.WM_ACTIVATE:
                    UpdateActiveProperty();
                    break;
                case PInvoke.WindowMessage.WM_DPICHANGED:
                    UpdateScaleProperty();
                    UpdateBorderThinessProperty();
                    var rect = Marshal.PtrToStructure<PInvoke.RECT>(lParam);
                    PInvoke.SetWindowPos(hWnd, 0, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, PInvoke.SetWindowPosFlags.SWP_NOZORDER);
                    break;
            }
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as FrameWindow;
            if (target.isInternalChange)
            {
                if (e.Property == LeftProperty || e.Property == TopProperty)
                {
                    target.OnLocationChanged(EventArgs.Empty);
                }
                else if (e.Property == WidthProperty || e.Property == HeightProperty)
                {
                    target.OnSizeChanged(EventArgs.Empty);
                }
                else if (e.Property == StateProperty)
                {
                    target.OnStateChanged(EventArgs.Empty);
                }
                else if (e.Property == ScaleProperty)
                {
                    target.OnScaleChanged(EventArgs.Empty);
                }
                else if (e.Property == IsActivedProperty)
                {
                    target.OnIsActivedChanged(EventArgs.Empty);
                }
            }
            else
            {
                if (e.Property == LeftProperty || e.Property == TopProperty ||
                    e.Property == WidthProperty || e.Property == HeightProperty)
                {
                    target.SetWindowPos();
                }
                else if (e.Property == StateProperty)
                {
                    target.SetWindowState();
                }
                else if (e.Property == TitleProperty)
                {
                    target.SetWindowTitle();
                }
            }
        }

        private void UpdatePosProperty()
        {
            isInternalChange = true;
            PInvoke.GetWindowRect(Handle, out var rect);
            Left = rect.left / Scale;
            Top = rect.top / Scale;
            Width = (rect.right - rect.left) / Scale;
            Height = (rect.bottom - rect.top) / Scale;
            isInternalChange = false;
        }

        private void UpdateStateProperty()
        {
            isInternalChange = true;
            PInvoke.GetWindowPlacement(Handle, out var placement);
            State = placement.showCmd switch
            {
                PInvoke.ShowWindowCommand.ShowMinimized => WindowState.Minimized,
                PInvoke.ShowWindowCommand.ShowMaximized => WindowState.Maximized,
                PInvoke.ShowWindowCommand.Minimize => WindowState.Minimized,
                _ => WindowState.Normal
            };
            isInternalChange = false;
        }

        private void UpdateBorderThinessProperty()
        {
            isInternalChange = true;
            BorderThiness = (PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CXFRAME) + PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CXPADDEDBORDER)) / Scale;
            isInternalChange = false;
        }

        private void UpdateScaleProperty()
        {
            isInternalChange = true;
            Scale = PInvoke.GetDpiForWindow(Handle) / 96d;
            isInternalChange = false;
        }

        private void UpdateActiveProperty()
        {
            isInternalChange = true;
            IsActived = PInvoke.GetForegroundWindow() == Handle;
            isInternalChange = false;
        }

        private void SetWindowPos()
        {
            if (Handle == IntPtr.Zero)
                return;
            var rawLeft = (int)(Left * Scale);
            var rawTop = (int)(Top * Scale);
            var rawWidth = (int)(Width * Scale);
            var rawHeight = (int)(Height * Scale);
            PInvoke.SetWindowPos(Handle, IntPtr.Zero, rawLeft, rawTop, rawWidth, rawHeight, 0);
        }

        private void SetStartupPos()
        {
            if (Handle == IntPtr.Zero)
                return;
            var rawLeft = (int)(Left * Scale);
            var rawTop = (int)(Top * Scale);
            var rawWidth = (int)(Width * Scale);
            var rawHeight = (int)(Height * Scale);
            switch (StartupLocation)
            {
                case WindowStartupLocation.CenterScreen:
                    var cx = PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CXSCREEN);
                    var cy = PInvoke.GetSystemMetrics(PInvoke.SystemMetric.SM_CYSCREEN) - (int)(40 * Scale);
                    rawLeft = (cx - rawWidth) / 2;
                    rawTop = (cy - rawHeight) / 2;
                    break;
            }
            PInvoke.SetWindowPos(Handle, IntPtr.Zero, rawLeft, rawTop, rawWidth, rawHeight, 0);
        }

        private void SetWindowState()
        {
            if (Handle == IntPtr.Zero)
                return;
            var showCmd = State switch
            {
                WindowState.Minimized => PInvoke.ShowWindowCommand.ShowMinimized,
                WindowState.Maximized => PInvoke.ShowWindowCommand.ShowMaximized,
                _ => PInvoke.ShowWindowCommand.Normal
            };
            PInvoke.ShowWindow(Handle, showCmd);
        }

        private void SetWindowTitle()
        {
            if (Handle == IntPtr.Zero)
                return;
            PInvoke.SetWindowText(Handle, Title);
        }
    }

    public enum WindowState
    {
        Normal, Minimized, Maximized
    }

    public enum WindowStartupLocation
    {
        Manual, CenterScreen, CenterOwner
    }
}
