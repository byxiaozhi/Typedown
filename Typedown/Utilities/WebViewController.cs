using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.Devices.Input;
using Windows.System;
using Windows.UI.Input;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using System.Reactive.Disposables;
using System.Diagnostics;
using Typedown.Windows;
using Windows.UI.Core;

namespace Typedown.Utilities
{
    public class WebViewController : IDisposable
    {
        public FrameworkElement Container { get; private set; }

        public IntPtr ParentHWnd { get; private set; }

        private static uint webView2ProcessId;

        private double WindowScale => PInvoke.GetDpiForWindow(ParentHWnd) / 96d;

        private static Task<CoreWebView2Environment> coreWebView2EnvironmentTask;
        private static CoreWebView2Environment CoreWebView2Environment => coreWebView2EnvironmentTask.IsCompleted ? coreWebView2EnvironmentTask.Result : null;

        private Task<CoreWebView2CompositionController> coreWebView2CompositionControllerTask;
        private CoreWebView2CompositionController CoreWebView2CompositionController => coreWebView2CompositionControllerTask.IsCompleted ? coreWebView2CompositionControllerTask.Result : null;

        public CoreWebView2Controller CoreWebView2Controller { get; private set; }

        public CoreWebView2 CoreWebView2 => CoreWebView2Controller?.CoreWebView2;

        private ContainerVisual webViewVisual;

        private readonly CompositeDisposable disposables = new();

        public async Task<bool> InitializeAsync(FrameworkElement container, nint parentHWnd)
        {
            if (Container == null)
            {
                try
                {
                    Container = container;
                    ParentHWnd = parentHWnd;
                    await EnsureCreateCompositionController();
                    await EnsureCreateController();
                    if (disposables.IsDisposed)
                    {
                        CoreWebView2Controller.Close();
                        throw new ObjectDisposedException(nameof(WebViewController));
                    }
                    InitializeEventHandler();
                    UpdataWindowScale();
                    disposables.Add(Observable.FromEventPattern(Container, nameof(Container.SizeChanged)).Subscribe(_ => UpdateBounds()));
                    var window = AppWindow.GetWindow(container);
                    disposables.Add(Observable.FromEventPattern(window, nameof(AppWindow.LocationChanged)).Subscribe(_ => UpdateBounds()));
                    disposables.Add(Observable.FromEventPattern(window, nameof(AppWindow.ScaleChanged)).Subscribe(_ => UpdataWindowScale()));
                    SetMaxWorkingSetSize();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        public static async Task<CoreWebView2Environment> EnsureCreateEnvironment()
        {
            if (coreWebView2EnvironmentTask == null)
            {
                var commandLineArgs = Universal.Config.WebView2Args.ToList();
#if DEBUG
                commandLineArgs.Add("--remote-debugging-port=9222");
#endif
                var options = new CoreWebView2EnvironmentOptions(string.Join(" ", commandLineArgs));
                coreWebView2EnvironmentTask = CoreWebView2Environment.CreateAsync(null, null, options);
                return await coreWebView2EnvironmentTask;
            }
            else
            {
                return await coreWebView2EnvironmentTask;
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

        public async Task<CoreWebView2Controller> EnsureCreateController()
        {
            if (CoreWebView2Controller == null)
            {
                CoreWebView2Controller = CreateCoreWebView2Controller(await coreWebView2CompositionControllerTask);
                CoreWebView2Controller.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            }
            return CoreWebView2Controller;
        }

        public static CoreWebView2Controller CreateCoreWebView2Controller(CoreWebView2CompositionController compositionController)
        {
            var raw = typeof(CoreWebView2CompositionController).GetField("_rawNative", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(compositionController);
            return typeof(CoreWebView2Controller).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(object) }, null).Invoke(new object[] { raw }) as CoreWebView2Controller;
        }

        private void InitializeEventHandler()
        {
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerEntered)).Subscribe(x => OnPointerEntered(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerMoved)).Subscribe(x => OnPointerMoved(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerPressed)).Subscribe(x => OnPointerPressed(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerReleased)).Subscribe(x => OnPointerReleased(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerWheelChanged)).Subscribe(x => OnPointerWheelChanged(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.PointerExited)).Subscribe(x => OnPointerExited(x.EventArgs as PointerRoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.GotFocus)).Subscribe(x => OnContainerGotFocus(x.EventArgs as RoutedEventArgs)));
            disposables.Add(Observable.FromEventPattern(Container, nameof(Container.GettingFocus)).Subscribe(x => OnContainerGettingFocus(x.EventArgs as GettingFocusEventArgs)));
            CoreWebView2Controller.LostFocus += OnCoreWebView2LostFocus;
            CoreWebView2Controller.MoveFocusRequested += OnCoreWebView2MoveFocusRequested;
            CoreWebView2CompositionController.CursorChanged += OnCoreWebView2CursorChanged;
        }

        private struct XamlFocusChangeInfo
        {
            public CoreWebView2MoveFocusReason storedMoveFocusReason;
            public bool isPending;
        };

        private XamlFocusChangeInfo xamlFocusChangeInfo;
        private bool webHasFocus;
        private bool hasMouseCapture;
        private bool hasPenCapture;
        private Dictionary<uint, bool> hasTouchCapture = new();
        private bool isLeftMouseButtonPressed;
        private bool isMiddleMouseButtonPressed;
        private bool isRightMouseButtonPressed;
        private bool isXButton1Pressed;
        private bool isXButton2Pressed;

        private void OnPointerEntered(PointerRoutedEventArgs args)
        {
            UpdateCursor();
        }

        private void OnPointerMoved(PointerRoutedEventArgs args)
        {
            var deviceType = args.Pointer.PointerDeviceType;
            var message = deviceType == PointerDeviceType.Mouse ? PInvoke.WindowMessage.WM_MOUSEMOVE : PInvoke.WindowMessage.WM_POINTERUPDATE;
            OnXamlPointerMessage(message, args);
        }

        private void OnPointerPressed(PointerRoutedEventArgs args)
        {
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

        private void OnPointerReleased(PointerRoutedEventArgs args)
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

        private void OnPointerWheelChanged(PointerRoutedEventArgs args)
        {
            var deviceType = args.Pointer.PointerDeviceType;
            var message = deviceType == PointerDeviceType.Mouse ? PInvoke.WindowMessage.WM_MOUSEWHEEL : PInvoke.WindowMessage.WM_POINTERWHEEL;
            OnXamlPointerMessage(message, args);
        }

        private void OnPointerExited(PointerRoutedEventArgs args)
        {
            CoreWindow.GetForCurrentThread().PointerCursor = new(0, 0);
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

        private void OnContainerGettingFocus(GettingFocusEventArgs args)
        {
            if (CoreWebView2 != null)
            {
                xamlFocusChangeInfo.storedMoveFocusReason = CoreWebView2MoveFocusReason.Programmatic;
                xamlFocusChangeInfo.isPending = true;
                if (args.InputDevice == FocusInputDeviceKind.Keyboard)
                {
                    if (args.Direction == FocusNavigationDirection.Next)
                    {
                        xamlFocusChangeInfo.storedMoveFocusReason = CoreWebView2MoveFocusReason.Next;
                    }
                    else if (args.Direction == FocusNavigationDirection.Previous)
                    {
                        xamlFocusChangeInfo.storedMoveFocusReason = CoreWebView2MoveFocusReason.Previous;
                    }
                }
            }
        }

        private void OnContainerGotFocus(RoutedEventArgs e)
        {
            if (CoreWebView2 != null && xamlFocusChangeInfo.isPending)
            {
                MoveFocusIntoCoreWebView(xamlFocusChangeInfo.storedMoveFocusReason);
                xamlFocusChangeInfo.isPending = false;
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
                var scale = (float)(1 / WindowScale);
                webViewVisual.Scale = new(scale, scale, 1);
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

        private static readonly IReadOnlyDictionary<nint, Lazy<CoreCursor>> coreCursorTypeDic = new Dictionary<nint, Lazy<CoreCursor>>()
        {
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW),  new(() => new(CoreCursorType.Arrow, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_CROSS),  new(() => new(CoreCursorType.Cross, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_HAND),  new(() => new(CoreCursorType.Hand, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_HELP),  new(() => new(CoreCursorType.Help, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_IBEAM),  new(() => new(CoreCursorType.IBeam, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZEALL),  new(() => new(CoreCursorType.SizeAll, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENESW),  new(() => new(CoreCursorType.SizeNortheastSouthwest, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENS),  new(() => new(CoreCursorType.SizeNorthSouth, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZENWSE),  new(() => new(CoreCursorType.SizeNorthwestSoutheast, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_SIZEWE),  new(() => new(CoreCursorType.SizeWestEast, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_NO),  new(() => new(CoreCursorType.UniversalNo, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_UPARROW),  new(() => new(CoreCursorType.UpArrow, 0))},
            {PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_WAIT),  new(() => new(CoreCursorType.Wait, 0))},
        };

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

        private void OnCoreWebView2LostFocus(object sender, object e)
        {
            webHasFocus = false;
        }

        public void MoveFocusIntoCoreWebView(CoreWebView2MoveFocusReason reason)
        {
            try
            {
                CoreWebView2Controller.MoveFocus(reason);
                webHasFocus = true;
            }
            catch (Exception)
            {
                webHasFocus = false;
            }
        }

        private void OnCoreWebView2MoveFocusRequested(object sender, CoreWebView2MoveFocusRequestedEventArgs e)
        {
            if (webHasFocus && e.Reason == CoreWebView2MoveFocusReason.Next || e.Reason == CoreWebView2MoveFocusReason.Previous)
            {
                var direction = e.Reason == CoreWebView2MoveFocusReason.Next ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous;
                var findNextElementOptions = new FindNextElementOptions() { SearchRoot = Container.XamlRoot.Content };
                FocusManager.TryMoveFocus(direction, findNextElementOptions);
            }
        }

        private void OnCoreWebView2CursorChanged(object sender, object e)
        {
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            coreCursorTypeDic.TryGetValue(CoreWebView2CompositionController.Cursor, out var cursor);
            CoreWindow.GetForCurrentThread().PointerCursor = cursor?.Value ?? new(0, 0);
        }

        private void SetMaxWorkingSetSize()
        {
            if (CoreWebView2 != null)
            {
                if (webView2ProcessId != CoreWebView2.BrowserProcessId)
                {
                    webView2ProcessId = CoreWebView2.BrowserProcessId;
                    if (Process.GetProcessById((int)webView2ProcessId) is Process process)
                        process.MaxWorkingSet = new(30 * 1024 * 1024);
                }
            }
        }

        public void Dispose()
        {
            if (!disposables.IsDisposed)
            {
                disposables.Dispose();
                CoreWebView2Controller?.Close();
            }
        }

        ~WebViewController()
        {
            Program.Dispatcher.InvokeAsync(Dispose);
        }
    }
}
