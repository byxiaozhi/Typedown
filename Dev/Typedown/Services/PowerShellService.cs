using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Typedown.Core.Interfaces;

namespace Typedown.Services
{
    /// <summary>
    /// Runs a script the person wrote (an image upload configuration's Upload-Image) in Windows PowerShell, which every
    /// Windows has. Upstream ran it in-process with the PowerShell SDK and then commented that out, leaving an exception:
    /// image upload did nothing at all. The script and the call go into a temporary .ps1 run by powershell.exe; its
    /// output lines come back.
    /// </summary>
    public class PowerShellService : IPowerShellService
    {
        private const int TimeoutMs = 60000;

        public IEnumerable<string> Invoke(string script, string command, params string[] parameters)
        {
            var file = Path.Combine(Path.GetTempPath(), $"upload-script-{Guid.NewGuid():N}.ps1");
            var call = command + " " + string.Join(" ", parameters.Select(p => "'" + p.Replace("'", "''") + "'"));
            // UTF-8 out, and a BOM in: Windows PowerShell 5.1 reads a script without one in the ANSI code page.
            File.WriteAllText(file, "[Console]::OutputEncoding = [Text.Encoding]::UTF8\r\n" + script + "\r\n" + call + "\r\n", new UTF8Encoding(true));
            try
            {
                var start = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{file}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                using var process = Process.Start(start) ?? throw new InvalidOperationException("powershell.exe did not start.");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(TimeoutMs))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException($"The script did not finish within {TimeoutMs / 1000} s.");
                }
                var lines = output.Result.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                if (process.ExitCode != 0 || lines.Count == 0)
                {
                    var message = error.Result.Trim();
                    throw new InvalidOperationException(message.Length > 0 ? message : $"The script returned nothing (exit code {process.ExitCode}).");
                }
                return lines;
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
