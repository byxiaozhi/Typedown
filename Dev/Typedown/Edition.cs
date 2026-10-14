using System;
using Microsoft.Extensions.DependencyInjection;
using Typedown.Core.Utilities;

namespace Typedown
{
    /// <summary>
    /// Where an edition built on Typedown (Typeleaf, the Microsoft Store edition) adds what only it has. Typedown leaves
    /// both methods unimplemented, so the compiler removes the calls and the application is exactly what it would be
    /// without them. An edition implements them in a file of its own, Dev/Typedown/Edition.&lt;name&gt;.cs (this project
    /// takes every .cs file, so no project file changes and a merge from Typedown never conflicts with it):
    ///
    ///     public static partial class Edition
    ///     {
    ///         static partial void RegisterServices(IServiceCollection services) { ... }
    ///         static partial void Initialize(IServiceProvider services) { ... }
    ///     }
    ///
    /// Tools/Branding/check-brand.py fails when Typedown itself carries such a file.
    /// </summary>
    public static partial class Edition
    {
        /// <summary>The edition's services, registered after the application's own (so it may replace one).</summary>
        static partial void RegisterServices(IServiceCollection services);

        /// <summary>Once the services are built, before the first window.</summary>
        static partial void Initialize(IServiceProvider services);

        // A failure in the edition's code is logged and the application starts without it, rather than not at all.
        internal static void Register(IServiceCollection services)
        {
            try { RegisterServices(services); }
            catch (Exception ex) { Log.WriteLocal("EditionRegisterServices", ex.ToString()); }
        }

        internal static void Start(IServiceProvider services)
        {
            try { Initialize(services); }
            catch (Exception ex) { Log.WriteLocal("EditionInitialize", ex.ToString()); }
        }

#if AUTOMATION_TEST_HOST
        /// <summary>
        /// The automation test host's test.edition.set: a name and a value for the edition to act on (a state it would
        /// otherwise get from outside, so a test can set it). Typedown has nothing to set.
        /// </summary>
        static partial void TestSet(string name, string value, ref bool handled);

        /// <summary>
        /// The window test.edition.set or test.edition.get was called for, while the edition handles it: what it acts on
        /// (an edition looking for "the newest window" itself found one closed but not yet collected).
        /// </summary>
        internal static Core.ViewModels.AppViewModel TestWindow { get; private set; }

        internal static bool TrySet(Core.ViewModels.AppViewModel window, string name, string value)
        {
            var handled = false;
            TestWindow = window;
            try { TestSet(name, value, ref handled); }
            finally { TestWindow = null; }
            return handled;
        }

        /// <summary>The automation test host's test.edition.get: a state of the edition's, by name, for a test to check.</summary>
        static partial void TestGet(string name, ref string value);

        internal static string TryGet(Core.ViewModels.AppViewModel window, string name)
        {
            string value = null;
            TestWindow = window;
            try { TestGet(name, ref value); }
            finally { TestWindow = null; }
            return value;
        }
#endif
    }
}
