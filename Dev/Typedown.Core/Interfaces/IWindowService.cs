using System;
using System.Reactive.Subjects;

namespace Typedown.Core.Interfaces
{
    /// <summary>
    /// Cross-platform window service interface.
    /// Platform implementations handle native window operations.
    /// </summary>
    public interface IWindowService
    {
        Subject<nint> WindowStateChanged { get; }

        Subject<nint> WindowIsActivedChanged { get; }
    }
}
