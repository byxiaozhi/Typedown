using System;
using System.Text.RegularExpressions;

namespace Typedown.Universal.Utilities
{
    public static class UriHelper
    {
        public static bool IsWebUrl(string str)
        {
            var regex = @"^http(s)?:\/\/([a-z0-9\-._~]+\.[a-z]{2,}|[0-9.]+|localhost|\[[a-f0-9.:]+\])(:[0-9]{1,5})?\/[\S]+";
            return Regex.IsMatch(str, regex, RegexOptions.IgnoreCase);
        }

        public static bool IsLocalUrl(string str)
        {
            var regex = @"^file:\/\/.+";
            return Regex.IsMatch(str, regex, RegexOptions.IgnoreCase);
        }

        public static bool IsAbsolutePath(string str)
        {
            var regex = @"^(?:\/|\\\\|[a-z]:\\|[a-z]:\/).+";
            return Regex.IsMatch(str, regex, RegexOptions.IgnoreCase);
        }
    }
}
