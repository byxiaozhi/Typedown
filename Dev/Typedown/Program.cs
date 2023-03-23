using System;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Typedown.Core.Utilities;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                App.Launch();
            }
            catch (Exception ex)
            {
                Task.Run(() => Common.Post("https://typedown.ownbox.cn/report", new
                {
                    version = AboutApp.GetAppVersion(),
                    system = Environment.OSVersion.VersionString,
                    type = "crash",
                    content = ex.ToString(),
                })).Wait();
                throw ex;
            }
        }
    }
}
