using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.Devices.Input;
using Windows.System;
using Windows.UI.Input;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Typedown.Controls;

namespace Typedown.Utilities
{
    public class WebViewController : IWebViewController, IDisposable
    {
        public FrameworkElement Container { get; private set; }

        public IntPtr ParentHWnd { get; private set; }

        private float WindowScale => PInvoke.GetDpiForWindow(ParentHWnd) / 96f;

        private static Task<CoreWebView2Environment> coreWebView2EnvironmentTask;
        private static CoreWebView2Environment CoreWebView2Environment => coreWebView2EnvironmentTask.IsCompleted ? coreWebView2EnvironmentTask.Result : null;

        private Task<CoreWebView2CompositionController> coreWebView2CompositionControllerTask;
        private CoreWebView2CompositionController CoreWebView2CompositionController => coreWebView2CompositionControllerTask.IsCompleted ? coreWebView2CompositionControllerTask.Result : null;

        public CoreWebView2Controller CoreWebView2Controller { get; private set; }

        public CoreWebView2 CoreWebView2 => CoreWebView2Controller?.CoreWebView2;

        private ContainerVisual webViewVisual;

        private bool isDisposed = false;

        public async Task InitializeAsync(FrameworkElement container, nint parentHWnd)
        {
            if (Container == null)
            {
                Container = container;
                ParentHWnd = parentHWnd;
                await EnsureCreateCompositionController();
                await EnsureCreateController();
                InitializeEventHandler();
                UpdataWindowScale();
                Observable.FromEventPattern(Container, nameof(Container.SizeChanged)).SubscribeWeak(_ => UpdateBounds());
                Observable.FromEventPattern(CoreWebView2CompositionController, nameof(CoreWebView2CompositionController.CursorChanged)).SubscribeWeak(_ => OnCursorChanged());
                var window = System.Windows.Window.GetWindow(AppXamlHost.GetAppXamlHost(container));
                Observable.FromEventPattern(window, nameof(System.Windows.Window.LocationChanged)).SubscribeWeak(_ => UpdateBounds());
                Observable.FromEventPattern(window, nameof(System.Windows.Window.DpiChanged)).SubscribeWeak(_ => UpdataWindowScale());
            }
        }

        private static async Task EnsureCreateEnvironment()
        {
            if (coreWebView2EnvironmentTask == null)
            {
                var commandLineArgs = new List<string>() { "--disable-web-security" };
#if DEBUG
                commandLineArgs.Add("--remote-debugging-port=9222");
#endif
                var options = new CoreWebView2EnvironmentOptions(string.Join(" ", commandLineArgs));
                coreWebView2EnvironmentTask = CoreWebView2Environment.CreateAsync(null, null, options);
                await coreWebView2EnvironmentTask;
            }
            else
            {
                await coreWebView2EnvironmentTask;
            }
        }

        private async Task EnsureCreateCompositionController()
        {
            if (coreWebView2CompositionControllerTask == null)
            {
                var compositor = ElementCompositionPreview.GetElementVisual(Container).Compositor;
                webViewVisual = compositor.CreateContainerVisual();
                ElementCompositionPreview.SetElementChildVisual(Container, webViewVisual);
                await EnsureCreateEnvironment();
                coreWebView2CompositionControllerTask = CoreWebView2Environment.CreateCoreWebView2CompositionControllerAsync(ParentHWnd);
                await coreWebView2CompositionControllerTask;
                CoreWebView2CompositionController.RootVisualTarget = webViewVisual;
            }
            else
            {
                await coreWebView2CompositionControllerTask;
            }
        }

        public async Task<bool> EnsureCreateController()
        {
            if (CoreWebView2Controller == null)
            {
                await coreWebView2CompositionControllerTask;
                var raw = typeof(CoreWebView2CompositionController).GetField("_rawNative", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CoreWebView2CompositionController);
                CoreWebView2Controller = typeof(CoreWebView2Controller).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(object) }, null).Invoke(new object[] { raw }) as CoreWebView2Controller;
                CoreWebView2Controller.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            }
            return CoreWebView2Controller != null;
        }

        private void InitializeEventHandler()
        {
            Observable.FromEventPattern(Container, nameof(Container.PointerMoved)).SubscribeWeak(x => OnPointerMoved(x.EventArgs as PointerRoutedEventArgs));
            Observable.FromEventPattern(Container, nameof(Container.PointerPressed)).SubscribeWeak(x => OnPointerPressed(x.EventArgs as PointerRoutedEventArgs));
            Observable.FromEventPattern(Container, nameof(Container.PointerReleased)).SubscribeWeak(x => OnPointerReleased(x.EventArgs as PointerRoutedEventArgs));
            Observable.FromEventPattern(Container, nameof(Container.PointerWheelChanged)).SubscribeWeak(x => OnPointerWheelChanged(x.EventArgs as PointerRoutedEventArgs));
            Observable.FromEventPattern(Container, nameof(Container.PointerExited)).SubscribeWeak(x => OnPointerExited(x.EventArgs as PointerRoutedEventArgs));
        }

        private void OnPointerMoved(PointerRoutedEventArgs args)
        {
            var deviceType = args.Pointer.PointerDeviceType;
            var message = deviceType == PointerDeviceType.Mouse ? PInvoke.WindowMessage.WM_MOUSEMOVE : PInvoke.WindowMessage.WM_POINTERUPDATE;
            OnXamlPointerMessage(message, args);
        }

        private bool hasMouseCapture;
        private bool hasPenCapture;
        private Dictionary<uint, bool> hasTouchCapture = new();
        private bool isLeftMouseButtonPressed;
        private bool isMiddleMouseButtonPressed;
        private bool isRightMouseButtonPressed;
        private bool isXButton1Pressed;
        private bool isXButton2Pressed;

        protected virtual void OnPointerPressed(PointerRoutedEventArgs args)
        {
            UpdateBounds();
            var deviceType = args.Pointer.PointerDeviceType;
            var pointerPoint = args.GetCurrentPoint(Container);
            PInvoke.WindowMessage message = 0;
            if (deviceType == PointerDeviceType.Mouse)
            {
                var properties = pointerPoint.Properties;
                hasMouseCapture = Container.CapturePointer(args.Pointer);
                if (properties.IsLeftButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_LBUTTONDOWN;
                    isLeftMouseButtonPressed = true;
                }
                else if (properties.IsMiddleButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_MBUTTONDOWN;
                    isMiddleMouseButtonPressed = true;
                }
                else if (properties.IsRightButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_RBUTTONDOWN;
                    isRightMouseButtonPressed = true;
                }
                else if (properties.IsXButton1Pressed)
                {
                    message = PInvoke.WindowMessage.WM_XBUTTONDOWN;
                    isXButton1Pressed = true;
                }
                else if (properties.IsXButton2Pressed)
                {
                    message = PInvoke.WindowMessage.WM_XBUTTONDOWN;
                    isXButton2Pressed = true;
                }
            }
            else if (deviceType == PointerDeviceType.Touch)
            {
                message = PInvoke.WindowMessage.WM_POINTERDOWN;
                hasTouchCapture.Add(pointerPoint.PointerId, Container.CapturePointer(args.Pointer));
            }
            else if (deviceType == PointerDeviceType.Pen)
            {
                message = PInvoke.WindowMessage.WM_POINTERDOWN;
                hasPenCapture = Container.CapturePointer(args.Pointer);
            }
            if (message != 0)
                OnXamlPointerMessage(message, args);
        }

        protected virtual void OnPointerReleased(PointerRoutedEventArgs args)
        {
            var deviceType = args.Pointer.PointerDeviceType;
            var pointerPoint = args.GetCurrentPoint(Container);
            PInvoke.WindowMessage message = 0;
            if (deviceType == PointerDeviceType.Mouse)
            {
                if (isLeftMouseButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_LBUTTONUP;
                    isLeftMouseButtonPressed = false;
                }
                else if (isMiddleMouseButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_MBUTTONUP;
                    isMiddleMouseButtonPressed = false;
                }
                else if (isRightMouseButtonPressed)
                {
                    message = PInvoke.WindowMessage.WM_RBUTTONUP;
                    isRightMouseButtonPressed = false;
                }
                else if (isXButton1Pressed)
                {
                    message = PInvoke.WindowMessage.WM_XBUTTONUP;
                    isXButton1Pressed = false;
                }
                else if (isXButton2Pressed)
                {
                    message = PInvoke.WindowMessage.WM_XBUTTONUP;
                    isXButton2Pressed = false;
                }
                if (hasMouseCapture)
                {
                    Container.ReleasePointerCapture(args.Pointer);
                    hasMouseCapture = false;
                }
            }
            else
            {
                if (hasTouchCapture.Keys.Contains(pointerPoint.PointerId))
                {
                    Container.ReleasePointerCapture(args.Pointer);
                    hasTouchCapture.Remove(pointerPoint.PointerId);
                }
                if (hasPenCapture)
                {
                    Container.ReleasePointerCapture(args.Pointer);
                    hasPenCapture = false;
                }
                message = PInvoke.WindowMessage.WM_POINTERUP;
            }
            if (message != 0)
                OnXamlPointerMessage(message, args);
        }

        protected virtual void OnPointerWheelChanged(PointerRoutedEventArgs args)
        {
            var deviceType = args.Pointer.PointerDeviceType;
            var message = deviceType == PointerDeviceType.Mouse ? PInvoke.WindowMessage.WM_MOUSEWHEEL : PInvoke.WindowMessage.WM_POINTERWHEEL;
            OnXamlPointerMessage(message, args);
        }

        protected virtual void OnPointerExited(PointerRoutedEventArgs args)
        {
            global::Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerCursor = new global::Windows.UI.Core.CoreCursor(0, 0);
            if (args.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                OnXamlPointerMessage(PInvoke.WindowMessage.WM_MOUSELEAVE, args);
                if (!hasMouseCapture) ResetMouseInputState();
            }
            else
            {
                OnXamlPointerMessage(PInvoke.WindowMessage.WM_POINTERLEAVE, args);
            }
        }

        public void UpdateBounds()
        {
            if (CoreWebView2Controller != null)
            {
                var p = Container.TransformToVisual(Container.XamlRoot.Content).TransformPoint(new());
                var s = Container.ActualSize;
                CoreWebView2Controller.Bounds = new(new((int)(p.X * WindowScale), (int)(p.Y * WindowScale)), new((int)(s.X * WindowScale), (int)(s.Y * WindowScale)));
            }
        }

        public void UpdataWindowScale()
        {
            if (webViewVisual != null)
            {
                webViewVisual.Scale = new(new(1 / WindowScale), 1);
                UpdateBounds();
            }
        }

        private void ResetMouseInputState()
        {
            isLeftMouseButtonPressed = false;
            isMiddleMouseButtonPressed = false;
            isRightMouseButtonPressed = false;
            isXButton1Pressed = false;
            isXButton2Pressed = false;
        }

        private void OnXamlPointerMessage(PInvoke.WindowMessage message, PointerRoutedEventArgs args)
        {
            if (CoreWebView2Controller == null)
                return;
            args.Handled = true;
            var logicalPointerPoint = args.GetCurrentPoint(Container);
            var logicalPoint = logicalPointerPoint.Position;
            var physicalPoint = new System.Drawing.Point((int)(logicalPoint.X * WindowScale), (int)(logicalPoint.Y * WindowScale));
            var deviceType = args.Pointer.PointerDeviceType;
            if (deviceType == PointerDeviceType.Mouse)
            {
                if (message == PInvoke.WindowMessage.WM_MOUSELEAVE)
                {
                    if (!hasMouseCapture)
                        CoreWebView2CompositionController.SendMouseInput((CoreWebView2MouseEventKind)message, 0, 0, new(0, 0));
                }
                else
                {
                    uint mouse_data = 0;
                    if (message == PInvoke.WindowMessage.WM_MOUSEWHEEL || message == PInvoke.WindowMessage.WM_MOUSEHWHEEL)
                    {
                        mouse_data = (uint)logicalPointerPoint.Properties.MouseWheelDelta;
                    }
                    if (message == PInvoke.WindowMessage.WM_XBUTTONDOWN || message == PInvoke.WindowMessage.WM_XBUTTONUP || message == PInvoke.WindowMessage.WM_XBUTTONDBLCLK)
                    {
                        var pointerUpdateKind = logicalPointerPoint.Properties.PointerUpdateKind;
                        if (pointerUpdateKind == PointerUpdateKind.XButton1Pressed || pointerUpdateKind == PointerUpdateKind.XButton1Released)
                            mouse_data |= 0x0001;
                        if (pointerUpdateKind == PointerUpdateKind.XButton2Pressed || pointerUpdateKind == PointerUpdateKind.XButton2Released)
                            mouse_data |= 0x0002;
                    }
                    CoreWebView2CompositionController.SendMouseInput((CoreWebView2MouseEventKind)message, GetKeyModifiers(args), mouse_data, physicalPoint);
                }
            }
            else
            {
                var inputPt = args.GetCurrentPoint(Container);
                var outputPt = CoreWebView2Environment.CreateCoreWebView2PointerInfo();
                FillPointerInfo(inputPt, outputPt, args);
                CoreWebView2CompositionController.SendPointerInput((CoreWebView2PointerEventKind)message, outputPt);
            }
        }

        private static readonly Dictionary<IntPtr, global::Windows.UI.Core.CoreCursorType> coreCursorTypeDic = new()
        {
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW),  global::Windows.UI.Core.CoreCursorType.Arrow},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_CROSS),  global::Windows.UI.Core.CoreCursorType.Cross},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_HAND),  global::Windows.UI.Core.CoreCursorType.Hand},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_HELP),  global::Windows.UI.Core.CoreCursorType.Help},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_IBEAM),  global::Windows.UI.Core.CoreCursorType.IBeam},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZEALL),  global::Windows.UI.Core.CoreCursorType.SizeAll},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENESW),  global::Windows.UI.Core.CoreCursorType.SizeNortheastSouthwest},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENS),  global::Windows.UI.Core.CoreCursorType.SizeNorthSouth},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENWSE),  global::Windows.UI.Core.CoreCursorType.SizeNorthwestSoutheast},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZEWE),  global::Windows.UI.Core.CoreCursorType.SizeWestEast},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_NO),  global::Windows.UI.Core.CoreCursorType.UniversalNo},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_UPARROW),  global::Windows.UI.Core.CoreCursorType.UpArrow},
            {PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_WAIT),  global::Windows.UI.Core.CoreCursorType.Wait}
        };

        private void OnCursorChanged()
        {
            if (coreCursorTypeDic.TryGetValue(CoreWebView2CompositionController.Cursor, out var cursor))
                global::Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerCursor = new global::Windows.UI.Core.CoreCursor(cursor, 0);
            else
                global::Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerCursor = new global::Windows.UI.Core.CoreCursor(0, 0);
        }

        private CoreWebView2MouseEventVirtualKeys GetKeyModifiers(PointerRoutedEventArgs args)
        {
            var properties = args.GetCurrentPoint(Container).Properties;
            var modifiers = CoreWebView2MouseEventVirtualKeys.None;
            if (args.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
                modifiers |= CoreWebView2MouseEventVirtualKeys.Control;
            if (args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
                modifiers |= CoreWebView2MouseEventVirtualKeys.Shift;
            if (properties.IsLeftButtonPressed)
                modifiers |= CoreWebView2MouseEventVirtualKeys.LeftButton;
            if (properties.IsRightButtonPressed)
                modifiers |= CoreWebView2MouseEventVirtualKeys.RightButton;
            if (properties.IsMiddleButtonPressed)
                modifiers |= CoreWebView2MouseEventVirtualKeys.MiddleButton;
            if (properties.IsXButton1Pressed)
                modifiers |= CoreWebView2MouseEventVirtualKeys.XButton1;
            if (properties.IsXButton2Pressed)
                modifiers |= CoreWebView2MouseEventVirtualKeys.XButton2;
            return modifiers;
        }

        private void FillPointerPenInfo(PointerPoint inputPt, CoreWebView2PointerInfo outputPt)
        {

            var inputProperties = inputPt.Properties;
            var outputPt_penFlags = PInvoke.PEN_FLAGS.PEN_FLAG_NONE;
            if (inputProperties.IsBarrelButtonPressed)
                outputPt_penFlags |= PInvoke.PEN_FLAGS.PEN_FLAG_BARREL;
            if (inputProperties.IsInverted)
                outputPt_penFlags |= PInvoke.PEN_FLAGS.PEN_FLAG_INVERTED;
            if (inputProperties.IsEraser)
                outputPt_penFlags |= PInvoke.PEN_FLAGS.PEN_FLAG_ERASER;
            outputPt.PenFlags = (uint)outputPt_penFlags;
            outputPt.PenMask = (uint)(PInvoke.PEN_MASK.PEN_MASK_PRESSURE | PInvoke.PEN_MASK.PEN_MASK_ROTATION | PInvoke.PEN_MASK.PEN_MASK_TILT_X | PInvoke.PEN_MASK.PEN_MASK_TILT_Y);
            outputPt.PenPressure = (uint)(inputProperties.Pressure * 1024);
            outputPt.PenRotation = (uint)inputProperties.Twist;
            outputPt.PenTiltX = (int)inputProperties.XTilt;
            outputPt.PenTiltY = (int)inputProperties.YTilt;
        }

        private void FillPointerTouchInfo(PointerPoint inputPt, CoreWebView2PointerInfo outputPt)
        {
            var inputProperties = inputPt.Properties;
            outputPt.TouchFlags = 0;
            outputPt.TouchMask = (uint)(PInvoke.TOUCH_MASK.TOUCH_MASK_CONTACTAREA | PInvoke.TOUCH_MASK.TOUCH_MASK_ORIENTATION | PInvoke.TOUCH_MASK.TOUCH_MASK_PRESSURE);
            var width = inputProperties.ContactRect.Width * WindowScale;
            var height = inputProperties.ContactRect.Height * WindowScale;
            var leftVal = inputProperties.ContactRect.X * WindowScale;
            var topVal = inputProperties.ContactRect.Y * WindowScale;
            outputPt.TouchContact = new System.Drawing.Rectangle((int)leftVal, (int)topVal, (int)width, (int)height);
            var widthRaw = inputProperties.ContactRectRaw.Width * WindowScale;
            var heightRaw = inputProperties.ContactRectRaw.Height * WindowScale;
            var leftValRaw = inputProperties.ContactRectRaw.X * WindowScale;
            var topValRaw = inputProperties.ContactRectRaw.Y * WindowScale;
            outputPt.TouchContactRaw = new System.Drawing.Rectangle((int)leftValRaw, (int)topValRaw, (int)widthRaw, (int)heightRaw);
            outputPt.TouchOrientation = (uint)inputProperties.Orientation;
            outputPt.TouchPressure = (uint)(inputProperties.Pressure * 1024);
        }

        private void FillPointerInfo(PointerPoint inputPt, CoreWebView2PointerInfo outputPt, PointerRoutedEventArgs args)
        {
            PointerPointProperties inputProperties = inputPt.Properties;
            var deviceType = inputPt.PointerDevice.PointerDeviceType;
            if (deviceType == PointerDeviceType.Pen)
                outputPt.PointerKind = (uint)PInvoke.POINTER_INPUT_TYPE.PT_MOUSE;
            else if (deviceType == PointerDeviceType.Touch)
                outputPt.PointerKind = (uint)PInvoke.POINTER_INPUT_TYPE.PT_MOUSE;
            outputPt.PointerId = args.Pointer.PointerId;
            outputPt.FrameId = inputPt.FrameId;
            var outputPt_pointerFlags = PInvoke.POINTER_FLAGS.POINTER_FLAG_NONE;
            if (inputProperties.IsInRange)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_INRANGE;
            if (deviceType == PointerDeviceType.Touch)
            {
                FillPointerTouchInfo(inputPt, outputPt);
                if (inputPt.IsInContact)
                {
                    outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_INCONTACT;
                    outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_FIRSTBUTTON;
                }
                if (inputProperties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
                {
                    outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_NEW;
                }
            }
            if (deviceType == PointerDeviceType.Pen)
            {
                FillPointerPenInfo(inputPt, outputPt);
                if (inputPt.IsInContact)
                {
                    outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_INCONTACT;
                    if (!inputProperties.IsBarrelButtonPressed)
                        outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_FIRSTBUTTON;
                    else
                        outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_SECONDBUTTON;
                }
            }

            if (inputProperties.IsPrimary)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_PRIMARY;
            if (inputProperties.TouchConfidence)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_CONFIDENCE;
            if (inputProperties.IsCanceled)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_CANCELED;
            if (inputProperties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_DOWN;
            if (inputProperties.PointerUpdateKind == PointerUpdateKind.Other)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_UPDATE;
            if (inputProperties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                outputPt_pointerFlags |= PInvoke.POINTER_FLAGS.POINTER_FLAG_UP;
            outputPt.PointerFlags = (uint)outputPt_pointerFlags;
            var outputPt_pointerPixelLocation = new System.Drawing.Point((int)(inputPt.Position.X * WindowScale), (int)(inputPt.Position.Y * WindowScale));
            outputPt.PixelLocation = outputPt_pointerPixelLocation;
            var outputPt_pointerRawPixelLocation = new System.Drawing.Point((int)(inputPt.RawPosition.X * WindowScale), (int)(inputPt.RawPosition.Y * WindowScale));
            outputPt.PixelLocationRaw = outputPt_pointerRawPixelLocation;
            var outputPoint_pointerTime = inputPt.Timestamp / 1000;
            outputPt.Time = (uint)outputPoint_pointerTime;
            var outputPoint_pointerHistoryCount = (uint)args.GetIntermediatePoints(Container).Count;
            outputPt.HistoryCount = outputPoint_pointerHistoryCount;
            if (PInvoke.QueryPerformanceFrequency(out var lpFrequency))
            {
                var scale = 1000000ul;
                var frequency = (ulong)lpFrequency;
                var outputPoint_pointerPerformanceCount = (inputPt.Timestamp * frequency) / scale;
                outputPt.PerformanceCount = outputPoint_pointerPerformanceCount;
            }
            outputPt.ButtonChangeKind = (int)inputProperties.PointerUpdateKind;
        }

        public void Navigate(string url) => CoreWebView2.Navigate(url);

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                CoreWebView2Controller.Close();
            }
        }

        ~WebViewController()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, Dispose);
        }
    }
}
