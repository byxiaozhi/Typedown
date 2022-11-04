using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Windows.System;

namespace Typedown.Services
{
    public class KeyboardAccelerator : IDisposable, IKeyboardAccelerator
    {
        private PInvoke.HookProc hookProc;

        private IntPtr hHook = IntPtr.Zero;

        private readonly Dictionary<ShortcutKey, HashSet<EventHandler<Universal.Models.KeyEventArgs>>> registeredDictionary = new();

        private readonly HashSet<EventHandler<Universal.Models.KeyEventArgs>> globalRegistered = new();

        public bool IsEnable
        {
            get { return hHook != IntPtr.Zero; }
            set { if (value) Start(); else Stop(); }
        }

        public void Start()
        {
            if (hHook == IntPtr.Zero)
            {
                hookProc ??= new(HookProc);
                var hInstance = PInvoke.LoadLibrary("User32");
                hHook = PInvoke.SetWindowsHookEx(PInvoke.HookType.WH_KEYBOARD_LL, hookProc, hInstance, 0);
            }
        }

        public void Stop()
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
                if (wParam == (nint)PInvoke.WindowMessage.WM_KEYDOWN)
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
            var eventArgs = new Universal.Models.KeyEventArgs(key, modifiers);
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
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers |= VirtualKeyModifiers.Menu;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers |= VirtualKeyModifiers.Control;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers |= VirtualKeyModifiers.Shift;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                modifiers |= VirtualKeyModifiers.Windows;
            return modifiers;
        }

        public IDisposable Register(ShortcutKey key, EventHandler<Universal.Models.KeyEventArgs> handler)
        {
            if (key == null || handler == null)
                return Disposable.Empty;
            if (!registeredDictionary.ContainsKey(key))
                registeredDictionary.Add(key, new());
            registeredDictionary[key].Add(handler);
            return Disposable.Create(() => registeredDictionary[key].Remove(handler));
        }

        public IDisposable RegisterGlobal(EventHandler<Universal.Models.KeyEventArgs> handler)
        {
            if (handler == null)
                return Disposable.Empty;
            globalRegistered.Add(handler);
            return Disposable.Create(() => globalRegistered.Remove(handler));
        }

        public string GetShortcutKeyText(ShortcutKey key)
        {
            if (key == null)
                return string.Empty;
            var result = new List<string>();
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Control))
                result.Add(GetVirtualKeyNameText(VirtualKey.Control));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
                result.Add(GetVirtualKeyNameText(VirtualKey.Menu));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
                result.Add(GetVirtualKeyNameText(VirtualKey.Shift));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Windows))
                result.Add("Win");
            result.Add(GetVirtualKeyNameText(key.Key));
            return string.Join('+', result);
        }

        public string GetVirtualKeyNameText(VirtualKey key)
        {
            if (key == VirtualKey.Delete) return "Delete";
            var buffer = new StringBuilder(32);
            var scanCode = PInvoke.MapVirtualKey((uint)key, PInvoke.MapVirtualKeyMapTypes.MAPVK_VK_TO_VSC);
            var lParam = scanCode << 16;
            PInvoke.GetKeyNameText(lParam, buffer, buffer.Capacity);
            return buffer.ToString();
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
