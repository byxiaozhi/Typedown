using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
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

        private VirtualKeyModifiers GetVirtualKeyModifiers()
        {
            var modifiers = VirtualKeyModifiers.None;
            if (PInvoke.GetIsKeyDown(VirtualKey.Menu))
                modifiers |= VirtualKeyModifiers.Menu;
            if (PInvoke.GetIsKeyDown(VirtualKey.Control))
                modifiers |= VirtualKeyModifiers.Control;
            if (PInvoke.GetIsKeyDown(VirtualKey.Shift))
                modifiers |= VirtualKeyModifiers.Shift;
            if (PInvoke.GetIsKeyDown(VirtualKey.LeftWindows) || PInvoke.GetIsKeyDown(VirtualKey.RightWindows))
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
