// Review F06: when the atomic replace keeps failing (a locked target), the write must NOT degrade to a
// non-atomic in-place copy. After its retries it must throw, leave the original file byte-for-byte intact, and
// keep the finished temp file as a recovery copy. Windows-only: it needs a real sharing lock on the target.
using Typedown.Core.Utilities;
using System.Text;

var dir = Path.Combine(Path.GetTempPath(), "safefilefail-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "doc.md");
var original = "ORIGINAL bytes that must survive a failed save\n";
var attempted = "NEW content that the locked save could not commit\n";
await File.WriteAllTextAsync(path, original, new UTF8Encoding(false));

int fails = 0;
void Assert(string name, bool ok) { Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name}"); if (!ok) fails++; }

// Hold the target open with no sharing so every atomic replace onto it fails.
bool threw = false;
using (var lockHandle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
{
    try { await SafeFile.WriteAllTextAtomicAsync(path, attempted); }
    catch { threw = true; }
}

Assert("the write reported failure (threw) instead of silently degrading", threw);
Assert("the original file is byte-for-byte intact", File.ReadAllText(path, new UTF8Encoding(false)) == original);

// The finished temp file (a hidden dot-file next to the target) must remain as a recovery copy.
var temps = Directory.GetFiles(dir, ".doc.md.*.tmp");
Assert("a complete temp file was kept for recovery", temps.Length >= 1);
if (temps.Length >= 1)
{
    var recovered = File.ReadAllText(temps[0], new UTF8Encoding(false));
    Assert("the kept temp file holds the content that could not be committed", recovered == attempted);
}

Directory.Delete(dir, true);
Console.WriteLine(fails == 0 ? "OK: a persistently locked replace fails safely and keeps a recovery copy" : $"FAIL: {fails} assertion(s) wrong");
return fails == 0 ? 0 : 1;
