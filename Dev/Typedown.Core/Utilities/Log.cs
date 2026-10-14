using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Config = Typedown.Core.Config;

namespace Typedown.Core.Utilities
{
    public static class Log
    {
        /// <summary>
        /// %LOCALAPPDATA%\Typedown\logs — crash reports are always written here, regardless of network settings. A package
        /// (the Microsoft Store edition) keeps them in its own folder, beside its settings: Windows 11 redirects a package's
        /// writes to %LOCALAPPDATA% into the package's private copy, which File Explorer does not see - "Open log folder"
        /// opened Documents instead.
        /// </summary>
        public static string LogFolder => Config.IsPackaged
            ? Path.Combine(Config.GetLocalFolderPath(), "logs")
            : Path.Combine(Config.LocalAppDataFolder, "logs");

        public static void WriteLocal(string type, string content)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var file = Path.Combine(LogFolder, $"{DateTime.Now:yyyyMMdd-HHmmss}-{type}.log");
                File.WriteAllText(file, $"Version: {AboutApp.GetAppVersion()}\nSystem: {Environment.OSVersion.VersionString}\nTime: {DateTime.Now:O}\nType: {type}\n\n{content}\n");
            }
            catch
            {
                // logging must never throw
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> pending = new();
        private static int writerRunning;

        /// <summary>
        /// Puts one line into %LOCALAPPDATA%\Typedown\logs\debug.log. The line is queued and written by a
        /// background task: opening and closing the file for every line used to cost minutes of UI-thread time
        /// when something chatty ran — walking a folder of tens of thousands of files, for instance.
        /// </summary>
        public static void Debug(string message)
        {
            pending.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {message}\n");
            if (System.Threading.Interlocked.Exchange(ref writerRunning, 1) == 1) return;
            Task.Run(async () =>
            {
                try
                {
                    Directory.CreateDirectory(LogFolder);
                    var file = Path.Combine(LogFolder, "debug.log");
                    PruneReportsOnce();
                    while (true)
                    {
                        var batch = new System.Text.StringBuilder();
                        while (pending.TryDequeue(out var line)) batch.Append(line);
                        if (batch.Length > 0)
                        {
                            RollOver(file);
                            try { File.AppendAllText(file, batch.ToString()); } catch { }
                        }
                        await Task.Delay(500);
                        if (pending.IsEmpty)
                        {
                            System.Threading.Interlocked.Exchange(ref writerRunning, 0);
                            // Anything queued between the check and the release is picked up by the next caller.
                            if (pending.IsEmpty) return;
                            if (System.Threading.Interlocked.Exchange(ref writerRunning, 1) == 1) return;
                        }
                    }
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref writerRunning, 0);
                }
            });
        }

        /// <summary>debug.log grows to this, then becomes debug.old.log (replacing the one before) and starts again.</summary>
        public const long DebugLogLimit = 2 * 1024 * 1024;

        /// <summary>Error reports (one file each) older than this go, and past this many only the newest stay.</summary>
        public static readonly TimeSpan ReportAge = TimeSpan.FromDays(30);
        public const int ReportCount = 50;

        // The logs had no limit: debug.log only ever grew (6.5 MB in two weeks on a machine used all day), and every
        // error report stayed for good. Two debug logs of 2 MB at most, and the error reports of the last month.
        private static void RollOver(string file)
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.Length < DebugLogLimit) return;
                var old = Path.Combine(LogFolder, "debug.old.log");
                File.Delete(old);
                File.Move(file, old);
            }
            catch
            {
                // another instance has it open, or is moving it this moment: written to as it is, moved next time
            }
        }

        private static int pruned;

        /// <summary>Once per run, when the first line is written: the error reports past their age or count go.</summary>
        private static void PruneReportsOnce()
        {
            if (System.Threading.Interlocked.Exchange(ref pruned, 1) == 1) return;
            try { PruneReports(LogFolder, DateTime.Now); } catch { }
        }

        /// <summary>The error reports in a folder (every *.log but the debug logs) past <see cref="ReportAge"/>, or beyond the newest <see cref="ReportCount"/>.</summary>
        public static int PruneReports(string folder, DateTime now)
        {
            var removed = 0;
            var reports = new DirectoryInfo(folder).GetFiles("*.log")
                .Where(f => !f.Name.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTime).ToList();
            for (var i = 0; i < reports.Count; i++)
            {
                if (i < ReportCount && now - reports[i].LastWriteTime <= ReportAge) continue;
                try { reports[i].Delete(); removed++; } catch { }
            }
            return removed;
        }

        /// <summary>
        /// An error worth keeping: written to the local log only. Nothing leaves the machine (the upstream app posted
        /// these to its own server).
        /// </summary>
        public static Task Report(string type, string content)
        {
            WriteLocal(type, content);
            return Task.CompletedTask;
        }
    }
}
