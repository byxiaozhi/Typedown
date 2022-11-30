using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Typedown.Universal.Utilities
{
    public static class UriHelper
    {
        public static bool IsWebUrl(string str)
        {
            try
            {
                var uri = new Uri(str);
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsLocalUrl(string str)
        {
            try
            {
                var uri = new Uri(str);
                return uri.Scheme == Uri.UriSchemeFile;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAbsolutePath(string str)
        {
            var regex = @"^(?:\/|\\\\|[a-z]:\\|[a-z]:\/).+";
            return Regex.IsMatch(str, regex, RegexOptions.IgnoreCase);
        }

        public static bool IsRelativePath(string str)
        {
            try
            {
                new Uri(str);
                return false;
            }
            catch
            {
                return !Path.IsPathRooted(str);
            }
        }

        public static bool TryGetLocalPath(string str, out string path)
        {
            if (IsAbsolutePath(str) || IsRelativePath(str))
            {
                path = str;
                return true;
            }
            else if (IsLocalUrl(str))
            {
                path = new Uri(str).LocalPath;
                return true;
            }
            path = null;
            return false;
        }
    }
}
