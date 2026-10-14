// Checks that TextFileFormat flags a lossy decode exactly when the file's bytes do not round-trip through the
// chosen encoding (review R07/F05). A non-UTF-8 file read as UTF-8 must be flagged so an automatic save cannot
// overwrite its original bytes; a valid UTF-8 file that genuinely contains U+FFFD must NOT be flagged.
using Typedown.Core.Utilities;
using System.Text;

var dir = Path.Combine(Path.GetTempPath(), "encoding-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
int fails = 0;

async Task Check(string name, byte[] bytes, bool expectLossy)
{
    var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".md");
    await File.WriteAllBytesAsync(path, bytes);
    var (_, format) = await TextFileFormat.ReadAsync(path);
    var ok = format.LossyDecode == expectLossy;
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name}: LossyDecode={format.LossyDecode} (expected {expectLossy})");
    if (!ok) fails++;
}

var utf8 = new UTF8Encoding(false);
await Check("valid utf8 ascii", utf8.GetBytes("hello world\n"), false);
await Check("valid utf8 cjk", utf8.GetBytes("# 标题\n正文 世界 😀\n"), false);
await Check("utf8 bom", new byte[] { 0xEF, 0xBB, 0xBF }.Concat(utf8.GetBytes("abc")).ToArray(), false);
await Check("utf16le bom", new UnicodeEncoding(false, true).GetPreamble().Concat(new UnicodeEncoding(false, false).GetBytes("hi 世界")).ToArray(), false);
await Check("gbk chinese bytes", new byte[] { 0xD5, 0xE2, 0xCA, 0xC7, 0xD6, 0xD0, 0xCE, 0xC4 }, true); // 这是中文 in GBK
await Check("invalid utf8 bytes", new byte[] { 0x48, 0x69, 0xFF, 0xFE, 0x80, 0x81 }, true);
await Check("genuine U+FFFD in valid utf8", utf8.GetBytes("ok � end"), false);
await Check("empty", Array.Empty<byte>(), false);

Directory.Delete(dir, true);
Console.WriteLine(fails == 0 ? "OK: lossy decode detected exactly when the bytes do not round-trip" : $"FAIL: {fails} case(s) wrong");
return fails == 0 ? 0 : 1;
