using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Typedown.WinUI.Utilities;
using WinRT;

namespace Typedown.WinUI
{
    // Replaces the WinUI 3 SDK-generated Main (see the #if !DISABLE_XAML_GENERATED_MAIN Program
    // class in the generated App.g.i.cs — that constant is now set project-wide in
    // Typedown.WinUI.csproj) so a second `Caret.exe` launch — e.g. double-clicking a .md file in
    // Explorer while Caret is already running — can be detected and redirected into the
    // already-running process before this process ever creates an Application or a window, instead
    // of starting a whole second instance.
    //
    // Ported in spirit from Typedown\App.cs's Mutex + NamedPipeServerStream handshake, using the
    // Windows App SDK's own AppInstance API (Microsoft.Windows.AppLifecycle) instead — the modern
    // replacement for that pattern. It ships in the Microsoft.WindowsAppSDK package this project
    // already references for WinUI 3 itself, works for unpackaged apps (not just MSIX), and needs no
    // separate pipe/mutex plumbing: FindOrRegisterForKey does the "am I first" check, and
    // RedirectActivationToAsync does the handoff.
    public static class Program
    {
        // Any string works as the key as long as it's unique to this app — it's a machine-wide name
        // (not scoped to install path or user), so it's namespaced with the app name the same way the
        // original's Mutex ("Typedown.App.Mutex") and pipe ("Typedown.App.PiPe") names were.
        private static readonly string InstanceKey = $"{Config.AppName}.SingleInstance";

        // Captured once the real UI thread's dispatcher exists (inside Application.Start's callback,
        // below) so OnActivated — which Microsoft.Windows.AppLifecycle raises on a thread-pool thread,
        // not the UI thread — has somewhere to marshal onto before touching any XAML object.
        private static DispatcherQueue uiDispatcherQueue;

        [STAThread]
        static void Main(string[] args)
        {
            ComWrappersSupport.InitializeComWrappers();
            // Started by Windows for File Explorer's right-click menu: no window, no XAML, just the command.
            if (Services.ExplorerCommandServer.IsServerLaunch(args))
            {
                Services.ExplorerCommandServer.Run();
                return;
            }
            if (DecideRedirection()) return;
            Application.Start(p =>
            {
                uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(uiDispatcherQueue);
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

        // Registers this process for InstanceKey. If nobody else holds it yet, this process becomes
        // "current" — it proceeds to start the app normally, exactly as before this feature existed
        // (App.OnLaunched still reads Environment.GetCommandLineArgs() itself for its own startup
        // file, same as always). If another process already holds the key, this process instead hands
        // its own activation args to that process and returns true so Main exits immediately, never
        // creating an Application or a window here.
        // The markdown file this process was started to open, from its activation (see
        // ExtractOpenFilePath) — null for a plain launch. Read by FileViewModel.LoadStartUpMarkdown.
        public static string StartupFilePath { get; private set; }

        // Files and folders File Explorer's "Convert to Markdown" started this process with, converted
        // once the first window is up (a launch that finds Caret running is redirected instead: OnActivated).
        public static System.Collections.Generic.List<string> StartupConvertPaths { get; set; }

        private static bool DecideRedirection()
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            StartupFilePath = ExtractOpenFilePath(activatedArgs);
            StartupConvertPaths = ExtractConvertPaths(activatedArgs);
            var keyInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (keyInstance.IsCurrent)
            {
                keyInstance.Activated += (s, args) => OnActivated(args);
                return false;
            }
            RedirectActivationTo(activatedArgs, keyInstance);
            return true;
        }

        // Runs the redirect on a worker thread and blocks this (STA) thread on an *alertable* wait
        // rather than a plain ManualResetEvent.WaitOne(). RedirectActivationToAsync makes a
        // cross-process COM call that needs to complete a callback back onto this same STA thread's
        // message queue — a non-alertable wait here would deadlock against it. This is Microsoft's own
        // documented shape for this API (Windows App SDK app-instancing samples), not improvised.
        private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
        {
            using var redirectedEvent = new ManualResetEvent(false);
            _ = Task.Run(() =>
            {
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
                redirectedEvent.Set();
            });
            Win32Event.WaitForSingleObjectEx(redirectedEvent.SafeWaitHandle.DangerousGetHandle(), 5000, true);
        }

        // Fires (on a thread-pool thread) whenever a later `Caret.exe` launch redirects into this
        // process. Mirrors Typedown\App.cs's ListenPipe handler: pull the markdown path, if any, out of
        // the second launch's own command line and hand it to the same open-or-focus logic "Open in
        // New Window"/Open Recent already use — not the current window's file, which would be wrong
        // for a redirect that isn't about this window at all.
        private static void OnActivated(AppActivationArguments args)
        {
            var convertPaths = ExtractConvertPaths(args);
            if (convertPaths != null)
            {
                uiDispatcherQueue?.TryEnqueue(() => MainWindow.ConvertFromShell(convertPaths));
                return;
            }
            var filePath = ExtractOpenFilePath(args);
            uiDispatcherQueue?.TryEnqueue(() => MainWindow.OpenOrFocus(filePath));
        }

        private static System.Collections.Generic.List<string> ExtractConvertPaths(AppActivationArguments args)
        {
            if (args.Kind != ExtendedActivationKind.Launch || args.Data is not Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs) return null;
            return CommandLine.GetConvertPaths(CommandLine.Split(launchArgs.Arguments));
        }

        private static string ExtractOpenFilePath(AppActivationArguments args)
        {
            // Opening a .md file through the installed package's file type association
            // (Package.appxmanifest's windows.fileTypeAssociation) arrives as a File activation, not a
            // Launch with the path on the command line.
            if (args.Kind == ExtendedActivationKind.File)
            {
                return args.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs
                    && fileArgs.Files.Count > 0 && FileTypeHelper.IsMarkdownFile(fileArgs.Files[0].Path)
                    ? fileArgs.Files[0].Path
                    : null;
            }
            if (args.Kind != ExtendedActivationKind.Launch) return null;
            // For a Launch-kind activation, Data comes back as the same classic UWP activation
            // interface the Windows App SDK reuses here rather than defining its own — confirmed
            // against the Microsoft.Windows.AppLifecycle.Projection assembly, which has no
            // ILaunchActivatedEventArgs of its own (it's projected from Windows.ApplicationModel
            // .Activation instead, available on this net8.0-windows10.0.19041.0 target the same way
            // Windows.Storage.Pickers already is elsewhere in this project).
            if (args.Data is not Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs) return null;
            // Arguments is the raw command-line string of the redirected launch (same shape as
            // Environment.CommandLine for a normal process start, exe path included) — split it the
            // same way the OS would, then reuse the existing CommandLine.GetOpenFilePath filter.
            return CommandLine.GetOpenFilePath(CommandLine.Split(launchArgs.Arguments));
        }
    }
}
