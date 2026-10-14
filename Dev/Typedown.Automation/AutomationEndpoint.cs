using System;
using System.Linq;

namespace Typedown.Automation
{
    /// <summary>
    /// Where the service listens. The name carries the current user's id (the SID on Windows), so users of one machine
    /// never share an endpoint, and the automation test host has a name of its own so it never answers for, or is
    /// reached instead of, the application. On Windows this is a named pipe that only the current user may open; on
    /// Unix .NET maps the same name to a socket in the temp directory (the Uno port uses its own 0600 socket).
    /// </summary>
    public static class AutomationEndpoint
    {
        public const string ApplicationPrefix = Brand.Name + ".Automation.v1";
        public const string TestHostPrefix = Brand.Name + ".AutomationTestHost.v1";

        public static string PipeName(string buildType, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user id is required.", nameof(userId));
            var safe = new string(userId.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            return (buildType == BuildTypes.AutomationTestHost ? TestHostPrefix : ApplicationPrefix) + "." + safe;
        }
    }
}
