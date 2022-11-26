using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Typedown.UITest
{
    [TestClass]
    public class UnitTest1: TestBase
    {
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            InitializeEnvironment(context);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupEnvironment();
        }

        [TestMethod]
        public void TestMethod1()
        {
            var menuBarFileItem = Session.FindElementByAccessibilityId("MenuBarFileItem");
            menuBarFileItem.Click();
            // Assert.IsNotNull(WindowRootLayout);
        }
    }
}
