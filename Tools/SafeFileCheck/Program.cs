using Typedown.Core.Utilities;
var dir = Path.Combine(Path.GetTempPath(), "safefile-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "doc.md");
await File.WriteAllTextAsync(path, "original content that must survive\n");
await SafeFile.WriteAllTextAtomicAsync(path, "second version\n");
var ok1 = File.ReadAllText(path) == "second version\n" && Directory.GetFiles(dir).Length == 1;
Console.WriteLine($"  normal write: {(ok1 ? "replaced, no temp file left" : "WRONG")}");
// Failure injection that works for root too: a name so long that the temporary file's name (the target's
// name plus a suffix) exceeds the file system's limit. The target itself still fits and exists.
var longPath = Path.Combine(dir, new string('x', 245) + ".md");
await File.WriteAllTextAsync(longPath, "second version\n");
var threw = false; try { await SafeFile.WriteAllTextAtomicAsync(longPath, ""); } catch { threw = true; }
path = longPath;
var ok2 = threw && File.ReadAllText(path) == "second version\n";
Console.WriteLine($"  failing write: {(ok2 ? "threw, file still holds the previous version" : "WRONG: " + (threw ? "content changed" : "did not throw"))}");
Console.WriteLine(ok1 && ok2 ? "OK: a failed save never truncates the file" : "FAIL");
Directory.Delete(dir, true);
return ok1 && ok2 ? 0 : 1;
