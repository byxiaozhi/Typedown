using Typedown.Core.Utilities;

// debug.log written past its limit: rolled over to debug.old.log, never more than two files of about the limit.
var root = Path.Combine(Path.GetTempPath(), "loglimit-" + Guid.NewGuid().ToString("N"));
Typedown.Core.Config.LocalAppDataFolder = root;
var line = new string('x', 1000);
for (var round = 0; round < 6; round++)
{
    for (var i = 0; i < 1100; i++) Log.Debug(line);  // a little over 1 MB a round
    await Task.Delay(1500);                          // the writer flushes every 500 ms
}
await Task.Delay(1500);
var debug = new FileInfo(Path.Combine(Log.LogFolder, "debug.log"));
var old = new FileInfo(Path.Combine(Log.LogFolder, "debug.old.log"));
var total = debug.Length + (old.Exists ? old.Length : 0);
var ok1 = old.Exists && debug.Length <= Log.DebugLogLimit + 2_000_000 && total <= 3 * Log.DebugLogLimit;
Console.WriteLine($"  ~6.6 MB written: debug.log {debug.Length / 1024} KB, debug.old.log {(old.Exists ? old.Length / 1024 + " KB" : "none")}: {(ok1 ? "rolled over, bounded" : "WRONG")}");

// Error reports: past 30 days, or beyond the newest 50, removed; the debug logs left alone.
var now = DateTime.Now;
for (var i = 0; i < 60; i++)
{
    var f = Path.Combine(Log.LogFolder, $"report-{i:00}.log");
    File.WriteAllText(f, "x");
    File.SetLastWriteTime(f, now - TimeSpan.FromDays(i < 40 ? i * 0.5 : 31 + i));  // 40 recent (0 to 19.5 days), 20 older than a month
}
for (var i = 0; i < 15; i++) File.SetLastWriteTime(Path.Combine(Log.LogFolder, $"report-{i:00}.log"), now - TimeSpan.FromMinutes(i));
var removed = Log.PruneReports(Log.LogFolder, now);
var left = Directory.GetFiles(Log.LogFolder, "report-*.log").Length;
var ok2 = removed == 20 && left == 40 && debug.Exists && File.Exists(old.FullName);
Console.WriteLine($"  60 reports, 20 older than 30 days: {removed} removed, {left} left, debug logs kept: {(ok2 ? "right" : "WRONG")}");
for (var i = 0; i < 70; i++) File.WriteAllText(Path.Combine(Log.LogFolder, $"new-{i:00}.log"), "x");
Log.PruneReports(Log.LogFolder, DateTime.Now);
var count = Directory.GetFiles(Log.LogFolder, "*.log").Count(f => !Path.GetFileName(f).StartsWith("debug"));
var ok3 = count == Log.ReportCount;
Console.WriteLine($"  110 recent reports: {count} kept: {(ok3 ? "the newest 50" : "WRONG")}");
Console.WriteLine(ok1 && ok2 && ok3 ? "OK: the logs stay bounded" : "FAIL");
Directory.Delete(root, true);
return ok1 && ok2 && ok3 ? 0 : 1;

// What Log.cs needs from the app, standing in for it.
namespace Typedown.Core
{
    public static class Config { public static string LocalAppDataFolder { get; set; } }
}
namespace Typedown.Core.Controls
{
    public static class AboutApp { public static string GetAppVersion() => "check"; }
}
