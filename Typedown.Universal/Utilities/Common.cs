using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Models;
using Windows.System;

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

        public static async Task<JObject> Post(string url, object obj)
        {
            var client = new HttpClient();
            var content = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
            var result = await client.PostAsync(url, content);
            if (result.StatusCode != HttpStatusCode.OK)
                throw new Exception(result.ReasonPhrase);
            return JObject.Parse(await result.Content.ReadAsStringAsync());
        }

        public static string GetShortcutKeyText(this ShortcutKey key)
        {
            return string.Join('+', GetShortcutKeyTextList(key));
        }

        public static List<string> GetShortcutKeyTextList(this ShortcutKey key)
        {
            if (key == null) return new();
            var result = new List<string>();
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Control))
                result.Add(GetVirtualKeyNameText(VirtualKey.Control));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
                result.Add(GetVirtualKeyNameText(VirtualKey.Menu));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
                result.Add(GetVirtualKeyNameText(VirtualKey.Shift));
            if (key.Modifiers.HasFlag(VirtualKeyModifiers.Windows))
                result.Add("Win");
            result.Add(GetVirtualKeyNameText(key.Key));
            return result;
        }

        public static string GetVirtualKeyNameText(this VirtualKey key)
        {
            if (key == VirtualKey.Delete)
                return "Delete";
            if (key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows)
                return "Win";
            var buffer = new StringBuilder(32);
            var scanCode = PInvoke.MapVirtualKey((uint)key, PInvoke.MapVirtualKeyMapTypes.MAPVK_VK_TO_VSC);
            var lParam = scanCode << 16;
            PInvoke.GetKeyNameText(lParam, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        public static void CopyProperties<T>(this T source, T target)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    var oldValue = prop.GetValue(target);
                    var newValue = prop.GetValue(source);
                    if (!(oldValue?.Equals(newValue) ?? oldValue == newValue))
                        prop.SetValue(target, newValue);
                }
            }
        }
    }
}
