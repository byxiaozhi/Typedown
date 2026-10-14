using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    /// <summary>How a window shows its document (window.getView / window.setView).</summary>
    public sealed class ViewState
    {
        /// <summary><c>visual</c>, <c>source</c> or <c>reading</c>.</summary>
        public string Mode { get; set; } = "visual";
        public bool SidePaneOpen { get; set; }
        /// <summary><c>files</c> or <c>outline</c>.</summary>
        public string SidePanePage { get; set; } = "files";
        public bool StatusBar { get; set; }
        public bool FocusMode { get; set; }
        public bool Typewriter { get; set; }
        /// <summary>The window's outer bounds in screen pixels.</summary>
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
    }

    /// <summary>What window.setView changes; null leaves that part as it is.</summary>
    public sealed class ViewChange
    {
        public string? Mode { get; set; }
        public bool? SidePaneOpen { get; set; }
        public string? SidePanePage { get; set; }
        public bool? StatusBar { get; set; }
        public bool? FocusMode { get; set; }
        public bool? Typewriter { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    /// <summary>
    /// What the view methods need from the application. A mode switch is serialized with the automation edits of the
    /// window's active document (DocumentEditCoordinator.ExclusiveAsync), and the result is read back after it applied.
    /// </summary>
    public interface IViewHost
    {
        Task<ViewState> GetViewAsync(string windowId, CancellationToken cancellationToken);
        Task<ViewState> SetViewAsync(string windowId, ViewChange change, CancellationToken cancellationToken);
    }

    /// <summary>window.getView and window.setView (docs/automation-api-spec.md, section 3.1).</summary>
    public static class ViewMethods
    {
        public const int MinWidth = 480, MinHeight = 320, MaxSize = 16384, MaxOffset = 32768;

        public static MethodTable AddTo(MethodTable table, IViewHost host)
        {
            table.Add(new MethodDescriptor("window.getView", Scopes.AppRead, "window.getView/1", async (c, ct) =>
                View(await host.GetViewAsync(c.Params.RequiredString("windowId", allowEmpty: false), ct).ConfigureAwait(false))));
            table.Add(new MethodDescriptor("window.setView", Scopes.WindowView, "window.setView/1", async (c, ct) =>
            {
                var p = c.Params;
                var windowId = p.RequiredString("windowId", allowEmpty: false);
                var change = new ViewChange
                {
                    Mode = p.Has("mode") ? p.OptionalEnum("mode", "visual", "visual", "source", "reading") : null,
                    StatusBar = p.Has("statusBar") ? p.OptionalBoolean("statusBar", false) : (bool?)null,
                    FocusMode = p.Has("focusMode") ? p.OptionalBoolean("focusMode", false) : (bool?)null,
                    Typewriter = p.Has("typewriter") ? p.OptionalBoolean("typewriter", false) : (bool?)null,
                };
                if (p.OptionalObject("sidePane") is JObject pane)
                {
                    var pp = new Params(pane);
                    if (pp.Has("open")) change.SidePaneOpen = pp.OptionalBoolean("open", false);
                    if (pp.Has("page")) change.SidePanePage = pp.OptionalEnum("page", "files", "files", "outline");
                }
                if (p.OptionalObject("bounds") is JObject bounds)
                {
                    var bp = new Params(bounds);
                    if (bp.Has("x")) change.X = (int)bp.RequiredInteger("x", -MaxOffset, MaxOffset);
                    if (bp.Has("y")) change.Y = (int)bp.RequiredInteger("y", -MaxOffset, MaxOffset);
                    if (bp.Has("width")) change.Width = (int)bp.RequiredInteger("width", MinWidth, MaxSize);
                    if (bp.Has("height")) change.Height = (int)bp.RequiredInteger("height", MinHeight, MaxSize);
                }
                if (change.Mode == null && change.StatusBar == null && change.FocusMode == null && change.Typewriter == null && change.SidePaneOpen == null
                    && change.SidePanePage == null && change.X == null && change.Y == null && change.Width == null && change.Height == null)
                    throw Params.Invalid("windowId", "nothingToChange", "window.setView needs at least one of mode, sidePane, statusBar, focusMode, typewriter, bounds.");
                return View(await host.SetViewAsync(windowId, change, ct).ConfigureAwait(false));
            }));
            return table;
        }

        private static JObject View(ViewState v) => new()
        {
            ["mode"] = v.Mode,
            ["sidePane"] = new JObject { ["open"] = v.SidePaneOpen, ["page"] = v.SidePanePage },
            ["statusBar"] = v.StatusBar,
            ["focusMode"] = v.FocusMode,
            ["typewriter"] = v.Typewriter,
            ["bounds"] = new JObject { ["x"] = v.X, ["y"] = v.Y, ["width"] = v.Width, ["height"] = v.Height },
            ["maximized"] = v.Maximized,
        };
    }
}
