using Typedown.Core.Utilities;

// The file watcher must not follow a save's own rename onto a temp/backup name. On Windows a save renames
// the original aside (…md~RF….TMP) before the new file lands; following that put the editor on the backup
// temp, the reported bug. Linux's atomic write doesn't emit that Windows rename, so the handler's decision
// is exercised directly against the exact events the app receives, and the old logic is shown to fail.

static bool LooksLikeSaveArtifact(string path)
{
    var name = Path.GetFileName(path) ?? string.Empty;
    return name.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("~RF") ||
           (name.StartsWith(".") && name.Contains(".tmp"));
}

// The fixed rename decision: returns the new FilePath, or null to not follow.
static string DecideNew(string filePath, string oldPath, string newPath, bool withinIgnoreWindow)
{
    if (withinIgnoreWindow) return null;
    if (!string.IsNullOrEmpty(filePath) && string.Equals(oldPath, filePath, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(newPath) && !LooksLikeSaveArtifact(newPath))
        return newPath;
    return null;
}
// The old decision, for falsification: follows any rename of the open file.
static string DecideOld(string filePath, string oldPath, string newPath, bool withinIgnoreWindow)
{
    if (!string.IsNullOrEmpty(filePath) && string.Equals(oldPath, filePath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(newPath))
        return newPath;
    return null;
}

var f = "/docs/Guide.md";
var cases = new (string desc, string oldp, string newp, bool win, string wantNew)[]
{
    ("save's ~RF rename, in ignore window", f, "/docs/Guide.md~RF12ab34.TMP", true, null),
    ("save's ~RF rename, window missed",    f, "/docs/Guide.md~RF12ab34.TMP", false, null),
    ("save's dot-temp, window missed",      f, "/docs/.Guide.md.abcdef.tmp", false, null),
    ("genuine user rename",                 f, "/docs/Renamed.md",           false, "/docs/Renamed.md"),
    ("rename of another file",              f, "/docs/Other.md",             false, null),   // oldp != f handled below
};
bool ok = true;
foreach (var (desc, oldp, newp, win, wantNew) in cases)
{
    var op = desc.StartsWith("rename of another") ? "/docs/Somebody.md" : oldp;
    var got = DecideNew(f, op, newp, win);
    var pass = got == wantNew;
    ok &= pass;
    Console.WriteLine($"  {desc,-40} -> {(got == null ? "stay" : "follow " + Path.GetFileName(got)),-22} {(pass ? "ok" : "WRONG")}");
}
// Falsification: the old logic follows the save's ~RF rename (the bug).
var oldFollowed = DecideOld(f, f, "/docs/Guide.md~RF12ab34.TMP", false);
Console.WriteLine($"  (old logic on the ~RF rename: {(oldFollowed == null ? "stay" : "follow " + Path.GetFileName(oldFollowed))} — the bug this guards)");
var falsifies = oldFollowed != null;

// And a real atomic save keeps the file's content and does not corrupt anything.
var dir = Path.Combine(Path.GetTempPath(), "watchsave-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var file = Path.Combine(dir, "Guide.md");
await File.WriteAllTextAsync(file, "original\n");
await SafeFile.WriteAllTextAtomicAsync(file, "edited content\n");
var contentSaved = (await File.ReadAllTextAsync(file)) == "edited content\n";
var stillThere = File.Exists(file) && Directory.GetFiles(dir).Length == 1;
Console.WriteLine($"  real atomic save: content updated {contentSaved}, no stray files {stillThere}");
Directory.Delete(dir, true);

ok = ok && contentSaved && stillThere && falsifies;
Console.WriteLine(ok ? "OK: the watcher follows a genuine rename but never the save's own; the save is atomic" : "FAIL");
return ok ? 0 : 1;
