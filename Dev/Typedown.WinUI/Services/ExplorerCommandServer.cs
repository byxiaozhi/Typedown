using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading;
using Typedown.WinUI.Services.Conversion;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Services
{
    // New since the fork: "Convert to Markdown" in File Explorer's right-click menu.
    //
    // Explorer (including Windows 11's short menu) shows an item for a packaged app when the manifest names
    // a COM class that implements IExplorerCommand (Package.appxmanifest: windows.fileExplorerContextMenus
    // and a windows.comServer). That class is here, in Caret's own exe: Windows starts the exe with
    // -ExplorerCommandServer, Program.Main hands over to Run() before any window or XAML exists, Explorer
    // asks whether to show the item and what to call it, and on a click Invoke starts Caret with the
    // selected paths (Caret --convert "a.docx" "b.pdf"), which converts them on the Convert page exactly as
    // if they had been dropped there. The server stays a couple of minutes after its last call and exits.
    //
    // Written with .NET's COM source generators ([GeneratedComInterface]): no C++ project, and nothing is
    // loaded into Explorer itself (an out-of-process server, so a fault here can't take Explorer down).
    internal static partial class ExplorerCommandServer
    {
        public const string ServerArgument = "-ExplorerCommandServer";

        // The class the manifest names (and the registry entries used to test it without a package).
        public static readonly Guid CommandClass = new("6b0f7a2e-4c1d-4e39-9e5b-3d8a1c7f52a4");

        private static long lastActivity = Environment.TickCount64;
        private static int instances;

        public static bool IsServerLaunch(string[] args) => args.Any(a => string.Equals(a, ServerArgument, StringComparison.OrdinalIgnoreCase));

        // Registers the class and serves it until it has been idle for a while.
        public static void Run()
        {
            var thread = new Thread(Serve) { IsBackground = false };
            thread.SetApartmentState(ApartmentState.MTA); // no message loop needed: calls arrive on RPC threads
            thread.Start();
            thread.Join();
        }

        private static void Serve()
        {
            var comWrappers = new StrategyBasedComWrappers();
            var factory = comWrappers.GetOrCreateComInterfaceForObject(new CommandFactory(comWrappers), CreateComInterfaceFlags.None);
            var clsid = CommandClass;
            var hr = CoRegisterClassObject(in clsid, factory, ClsCtxLocalServer, RegClsMultipleUse, out var cookie);
            if (hr != 0)
            {
                Note($"couldn't register the class: 0x{hr:X8}");
                return;
            }
            var started = Environment.TickCount64;
            while (true)
            {
                Thread.Sleep(1000);
                var now = Environment.TickCount64;
                // Never asked for anything (started for a class that then wasn't wanted), or quiet since.
                if (Volatile.Read(ref instances) == 0 ? now - started > 30_000 : now - lastActivity > 120_000) break;
            }
            CoRevokeClassObject(cookie);
            Marshal.Release(factory);
        }

        internal static void Touch() => Interlocked.Exchange(ref lastActivity, Environment.TickCount64);

        // --- What to show ---

        // Files Caret can convert, and folders (converted with everything convertible inside them).
        internal static bool IsConvertible(string path) =>
            !string.IsNullOrEmpty(path) && (DocumentConverter.IsSupported(path) || Directory.Exists(path));

        // Whether the item appears at all: not when an administrator switched it off (policy), nor the user
        // (Convert page), and only for a selection that has something to convert.
        internal static bool ShouldShow(IReadOnlyList<string> paths)
        {
            if (Config.PolicyDisablesExplorerMenu) return false;
            try { if (!new ViewModels.SettingsViewModel().ExplorerMenu) return false; }
            catch { }
            return paths.Any(IsConvertible);
        }

        internal static string Title()
        {
            try { Locale.Load(new ViewModels.SettingsViewModel().Language); }
            catch { }
            return Locale.GetString("ExplorerMenuTitle");
        }

        // Starts Caret on the paths. A very long selection goes through a list file, as a command line
        // is limited to about 32,000 characters.
        internal static void Launch(IReadOnlyList<string> paths)
        {
            var convertible = paths.Where(IsConvertible).ToList();
            if (convertible.Count == 0) return;
            var arguments = "--convert " + string.Join(" ", convertible.Select(Quote));
            if (arguments.Length > 6000)
            {
                var list = Path.Combine(Path.GetTempPath(), $"caret-convert-{Guid.NewGuid():N}.txt");
                File.WriteAllLines(list, convertible, new UTF8Encoding(false));
                arguments = "--convert-list " + Quote(list);
            }
            Process.Start(new ProcessStartInfo(Environment.ProcessPath, arguments) { UseShellExecute = false });
        }

        // A path as one argument (a folder's trailing backslash would otherwise swallow the closing quote).
        internal static string Quote(string path) => "\"" + (path.EndsWith('\\') ? path + "\\" : path) + "\"";

        // --- COM ---

        private const uint ClsCtxLocalServer = 4;
        private const uint RegClsMultipleUse = 1;

        [LibraryImport("ole32.dll")]
        private static partial int CoRegisterClassObject(in Guid rclsid, IntPtr pUnk, uint dwClsContext, uint flags, out uint lpdwRegister);

        [LibraryImport("ole32.dll")]
        private static partial int CoRevokeClassObject(uint dwRegister);

        [GeneratedComInterface]
        [Guid("00000001-0000-0000-C000-000000000046")]
        internal partial interface IClassFactory
        {
            [PreserveSig]
            int CreateInstance(IntPtr outer, in Guid riid, out IntPtr instance);

            [PreserveSig]
            int LockServer([MarshalAs(UnmanagedType.Bool)] bool @lock);
        }

        [GeneratedComClass]
        internal sealed partial class CommandFactory : IClassFactory
        {
            private readonly StrategyBasedComWrappers comWrappers;

            public CommandFactory(StrategyBasedComWrappers comWrappers) => this.comWrappers = comWrappers;

            public int CreateInstance(IntPtr outer, in Guid riid, out IntPtr instance)
            {
                instance = IntPtr.Zero;
                Touch();
                if (outer != IntPtr.Zero) return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
                var unknown = comWrappers.GetOrCreateComInterfaceForObject(new ConvertCommand(), CreateComInterfaceFlags.None);
                var iid = riid;
                var hr = Marshal.QueryInterface(unknown, ref iid, out instance);
                Marshal.Release(unknown);
                if (hr == 0) Interlocked.Increment(ref instances);
                return hr;
            }

            public int LockServer(bool @lock)
            {
                Touch();
                return 0;
            }
        }

        [GeneratedComInterface]
        [Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
        internal partial interface IExplorerCommand
        {
            [PreserveSig] int GetTitle(IShellItemArray items, out IntPtr name);
            [PreserveSig] int GetIcon(IShellItemArray items, out IntPtr icon);
            [PreserveSig] int GetToolTip(IShellItemArray items, out IntPtr tip);
            [PreserveSig] int GetCanonicalName(out Guid name);
            [PreserveSig] int GetState(IShellItemArray items, [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow, out uint state);
            [PreserveSig] int Invoke(IShellItemArray items, IntPtr bindContext);
            [PreserveSig] int GetFlags(out uint flags);
            [PreserveSig] int EnumSubCommands(out IntPtr enumerator);
        }

        [GeneratedComInterface]
        [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
        internal partial interface IShellItemArray
        {
            [PreserveSig] int BindToHandler(IntPtr bindContext, in Guid handler, in Guid riid, out IntPtr result);
            [PreserveSig] int GetPropertyStore(uint flags, in Guid riid, out IntPtr result);
            [PreserveSig] int GetPropertyDescriptionList(IntPtr key, in Guid riid, out IntPtr result);
            [PreserveSig] int GetAttributes(uint flags, uint mask, out uint attributes);
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetItemAt(uint index, out IShellItem item);
            [PreserveSig] int EnumItems(out IntPtr enumerator);
        }

        [GeneratedComInterface]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        internal partial interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr bindContext, in Guid handler, in Guid riid, out IntPtr result);
            [PreserveSig] int GetParent(out IntPtr parent);
            [PreserveSig] int GetDisplayName(uint form, out IntPtr name);
            [PreserveSig] int GetAttributes(uint mask, out uint attributes);
            [PreserveSig] int Compare(IntPtr other, uint hint, out int order);
        }

        private const int NotImplemented = unchecked((int)0x80004001);
        private const uint FileSystemPath = 0x80058000; // SIGDN_FILESYSPATH

        [GeneratedComClass]
        internal sealed partial class ConvertCommand : IExplorerCommand
        {
            public int GetTitle(IShellItemArray items, out IntPtr name)
            {
                Touch();
                name = Marshal.StringToCoTaskMemUni(Title());
                return 0;
            }

            public int GetIcon(IShellItemArray items, out IntPtr icon)
            {
                Touch();
                icon = Marshal.StringToCoTaskMemUni(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico"));
                return 0;
            }

            public int GetToolTip(IShellItemArray items, out IntPtr tip)
            {
                tip = IntPtr.Zero;
                return NotImplemented;
            }

            public int GetCanonicalName(out Guid name)
            {
                name = CommandClass;
                return 0;
            }

            public int GetState(IShellItemArray items, bool okToBeSlow, out uint state)
            {
                Touch();
                // 0 enabled, 2 hidden (a disabled item would only be greyed out)
                state = ShouldShow(Paths(items)) ? 0u : 2u;
                return 0;
            }

            public int Invoke(IShellItemArray items, IntPtr bindContext)
            {
                Touch();
                try { Launch(Paths(items)); }
                catch (Exception ex) { LogError(ex); }
                return 0;
            }

            public int GetFlags(out uint flags)
            {
                flags = 0; // ECF_DEFAULT
                return 0;
            }

            public int EnumSubCommands(out IntPtr enumerator)
            {
                enumerator = IntPtr.Zero;
                return NotImplemented;
            }

            // The file-system paths of the selection (items that aren't files, such as a virtual folder, are skipped).
            private static List<string> Paths(IShellItemArray items)
            {
                var paths = new List<string>();
                try
                {
                    if (items == null || items.GetCount(out var count) != 0) return paths;
                    for (uint i = 0; i < count && i < 2000; i++)
                    {
                        if (items.GetItemAt(i, out var item) != 0 || item == null) continue;
                        if (item.GetDisplayName(FileSystemPath, out var name) != 0 || name == IntPtr.Zero) continue;
                        try { paths.Add(Marshal.PtrToStringUni(name)); }
                        finally { Marshal.FreeCoTaskMem(name); }
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
                return paths;
            }
        }

        private static void LogError(Exception ex) => Note(ex.ToString());

        private static void Note(string message)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "caret_winui_probe.log"), $"{DateTime.Now:O} ExplorerMenu: {message}\n"); }
            catch { }
        }
    }
}
