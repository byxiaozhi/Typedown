using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Typedown.Universal.Utilities
{
    public static class Common
    {
        public static T GetBreakPointValue<T>(this double width, T largeValue, T mediumValue, T smallValue)
        {
            if (width >= 1008)
                return largeValue;
            if (width >= 641)
                return mediumValue;
            return smallValue;
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static ulong SimpleHash(string str)
        {
            ulong hashedValue = 3074457345618258791ul;
            for (int i = 0; i < str.Length; i++)
            {
                hashedValue += str[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }

        public static string DefaultMarkdwn { get => "\n"; }
    }
}
