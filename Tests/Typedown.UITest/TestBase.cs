using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System;

namespace Typedown.UITest
{
    public class TestBase
    {
        private const string winAppDriverUrl = "http://127.0.0.1:4723";

        protected static WindowsDriver<WindowsElement> Session { get; private set; }

        public static void InitializeEnvironment(TestContext context)
        {
            if (Session == null)
            {
                var handle = GetTopLevelWindowHandle();
                var options = new AppiumOptions();
                options.AddAdditionalCapability("appTopLevelWindow", handle);
                Session = new WindowsDriver<WindowsElement>(new Uri(winAppDriverUrl), options);
            }
        }

        public static void CleanupEnvironment()
        {
            if (Session != null)
            {
                Session.Dispose();
                Session = null;
            }
        }

        private static string GetTopLevelWindowHandle()
        {
            var options = new AppiumOptions();
            options.AddAdditionalCapability("app", "Root");
            using var desktopSession = new WindowsDriver<WindowsElement>(new Uri(winAppDriverUrl), options);
            var typedownWindow = desktopSession.FindElementByClassName("Typedown.Windows.FrameWindow");
            var typedownWindowHandle = typedownWindow.GetAttribute("NativeWindowHandle");
            return int.Parse(typedownWindowHandle).ToString("x");
        }
    }
}
