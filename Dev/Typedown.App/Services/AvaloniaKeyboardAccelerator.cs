using System;
using System.Collections.Generic;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia IKeyboardAccelerator implementation.
/// Menu InputGestures handle the main shortcuts. This provides the Core interface.
/// </summary>
public class AvaloniaKeyboardAccelerator : IKeyboardAccelerator
{
    public IDisposable Register(ShortcutKey key, EventHandler<KeyEventArgs> handler)
        => System.Reactive.Disposables.Disposable.Empty;

    public IDisposable RegisterGlobal(EventHandler<KeyEventArgs> handler)
        => System.Reactive.Disposables.Disposable.Empty;

    public IObservable<KeyEventArgs> GetObservable()
        => System.Reactive.Linq.Observable.Empty<KeyEventArgs>();

    public string GetShortcutKeyText(ShortcutKey key)
    {
        var parts = new List<string>();
        if (key.Modifiers.HasFlag(VirtualKeyModifiers.Control)) parts.Add("Ctrl");
        if (key.Modifiers.HasFlag(VirtualKeyModifiers.Shift)) parts.Add("Shift");
        if (key.Modifiers.HasFlag(VirtualKeyModifiers.Menu)) parts.Add("Alt");
        parts.Add(key.Key.ToString());
        return string.Join("+", parts);
    }

    public string GetVirtualKeyNameText(VirtualKey key) => key.ToString();
}
