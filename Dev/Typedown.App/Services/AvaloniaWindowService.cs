using System;
using System.Reactive.Subjects;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia IWindowService implementation.
/// </summary>
public class AvaloniaWindowService : IWindowService
{
    public Subject<nint> WindowStateChanged { get; } = new();
    public Subject<nint> WindowIsActivedChanged { get; } = new();
}
