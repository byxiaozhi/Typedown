using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Windows.System;
using Windows.UI.Xaml;

namespace Typedown.Services
{
    public class KeyboardAccelerator : DependencyObject, IDisposable, IKeyboardAccelerator
    {
        public static DependencyProperty IsEnableProperty { get; } = DependencyProperty.Register(nameof(IsEnable), typeof(bool), typeof(KeyboardAccelerator), new(false, OnPropertyChanged));
        public bool IsEnable { get => (bool)GetValue(IsEnableProperty); set => SetValue(IsEnableProperty, value); }

        private PInvoke.HookProc hookProc;

        private IntPtr hHook = IntPtr.Zero;

        private readonly Dictionary<ShortcutKey, HashSet<EventHandler<KeyEventArgs>>> registeredDictionary = new();

        private readonly HashSet<EventHandler<KeyEventArgs>> globalRegistered = new();

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as KeyboardAccelerator;
            if (e.Property == IsEnableProperty)
            {
                if ((bool)e.NewValue) target.Start();
                else target.Stop();
            }
        }

        private void Start()
        {
            if (hHook == IntPtr.Zero)
            {
                hookProc ??= new(HookProc);
                var hInstance = PInvoke.LoadLibrary("User32");
                hHook = PInvoke.SetWindowsHookEx(PInvoke.HookType.WH_KEYBOARD_LL, hookProc, hInstance, 0);
            }
        }

        private void Stop()
        {
            if (hHook != IntPtr.Zero)
            {
                PInvoke.UnhookWindowsHookEx(hHook);
                hHook = IntPtr.Zero;
            }
        }

        private nint HookProc(int code, nint wParam, nint lParam)
        {
            if (code >= 0)
            {
                var msg = (PInvoke.WindowMessage)wParam;
                if (msg == PInvoke.WindowMessage.WM_KEYDOWN || msg == PInvoke.WindowMessage.WM_SYSKEYDOWN)
                {
                    var args = (PInvoke.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(PInvoke.KBDLLHOOKSTRUCT));
                    if (OnKeyBoardEvent(args)) return 1;
                }
            }
            return PInvoke.CallNextHookEx(hHook, code, wParam, lParam);
        }

        private bool OnKeyBoardEvent(PInvoke.KBDLLHOOKSTRUCT args)
        {
            var key = (VirtualKey)args.vkCode;
            var modifiers = GetVirtualKeyModifiers();
            var eventArgs = new KeyEventArgs(key, modifiers);
            foreach (var action in globalRegistered)
                action(this, eventArgs);
            if (registeredDictionary.TryGetValue(new(modifiers, key), out var actions))
                foreach (var action in actions)
                    action(this, eventArgs);
            return eventArgs.Handled;
        }

        // The modifiers as the system has them when this key arrives (GetIsKeyDownNow). Read as this thread had them
        // (GetKeyState), Ctrl was not down yet whenever the web view's thread had not read its press before Z came: Ctrl
        // and Z close together (a fast chord, a busy page, a tool sending the whole chord at once) were taken for a plain Z,
        // which went on to the page - the browser's own undo, apart from the host's history (and so Ctrl+B, Ctrl+1...).
        // The system's state is also right after a key let go elsewhere (another window, the lock screen).
        private VirtualKeyModifiers GetVirtualKeyModifiers()
        {
            var modifiers = VirtualKeyModifiers.None;
            if (PInvoke.GetIsKeyDownNow(VirtualKey.Menu))
                modifiers |= VirtualKeyModifiers.Menu;
            if (PInvoke.GetIsKeyDownNow(VirtualKey.Control))
                modifiers |= VirtualKeyModifiers.Control;
            if (PInvoke.GetIsKeyDownNow(VirtualKey.Shift))
                modifiers |= VirtualKeyModifiers.Shift;
            if (PInvoke.GetIsKeyDownNow(VirtualKey.LeftWindows) || PInvoke.GetIsKeyDownNow(VirtualKey.RightWindows))
                modifiers |= VirtualKeyModifiers.Windows;
            return modifiers;
        }

        public IDisposable Register(ShortcutKey key, EventHandler<KeyEventArgs> handler)
        {
            if (key == null || handler == null)
                return Disposable.Empty;
            if (!registeredDictionary.ContainsKey(key))
                registeredDictionary.Add(key, new());
            registeredDictionary[key].Add(handler);
            return Disposable.Create(() => registeredDictionary[key].Remove(handler));
        }

        public IDisposable RegisterGlobal(EventHandler<KeyEventArgs> handler)
        {
            if (handler == null)
                return Disposable.Empty;
            globalRegistered.Add(handler);
            return Disposable.Create(() => globalRegistered.Remove(handler));
        }

        public IObservable<KeyEventArgs> GetObservable()
        {
            return Observable.Create<KeyEventArgs>(o => RegisterGlobal((_, e) => o.OnNext(e)));
        }

        public string GetShortcutKeyText(ShortcutKey key)
        {
            return Common.GetShortcutKeyText(key);
        }

        public string GetVirtualKeyNameText(VirtualKey key)
        {
            return Common.GetVirtualKeyNameText(key);
        }

        public void Dispose()
        {
            Stop();
        }

        ~KeyboardAccelerator()
        {
            Dispose();
        }
    }
}
