using System;
using System.Threading.Tasks;

namespace Typedown.Core.Utilities
{
    public static class Log
    {
        public static string AppVersion { get; set; } = "0.0.0";

        public static Task Report(string type, string content)
        {
            return Task.Run(() => Common.Post("https://typedown.ownbox.cn/report", new
            {
                version = AppVersion,
                system = Environment.OSVersion.VersionString,
                type,
                content,
            }));
        }
    }
}
