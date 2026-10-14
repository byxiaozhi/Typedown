using System;
using Typedown.Automation;
using System.IO;

namespace Typedown.Mcp
{
    /// <summary>This server's own client id: a UUID made once and kept, like typedownctl's.</summary>
    public static class McpClientId
    {
        public static string Load()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), Brand.Name);
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "typedown-mcp-client-id");
                if (File.Exists(file) && Guid.TryParseExact(File.ReadAllText(file).Trim(), "D", out var existing)) return existing.ToString("D");
                var id = Guid.NewGuid().ToString("D");
                File.WriteAllText(file, id);
                return id;
            }
            catch
            {
                return Guid.NewGuid().ToString("D");
            }
        }
    }
}
