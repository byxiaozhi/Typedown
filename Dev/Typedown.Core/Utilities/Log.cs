using System;
using System.Threading.Tasks;
using Typedown.Core.Controls;

namespace Typedown.Core.Utilities
{
    public static class Log
    {
        public static Task Report(string type, string content)
        {
            return Task.Run(() => Common.Post("https://typedown.ownbox.cn/report", new
            {
                version = AboutApp.GetAppVersion(),
                system = Environment.OSVersion.VersionString,
                type,
                content,
            }));
        }
    }
}
