using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Typedown.Services;

namespace Typedown.Test.ServiceTests
{
    [TestClass]
    public class ClipboardTest
    {
        public Clipboard GetDefaultClipboardService()
        {
            return new Clipboard();
        }


        [STATestMethod]
        public void SetTextAndGetText_WithRandomText_CanSetAndGet()
        {
            var clipboard = GetDefaultClipboardService();
            var text = new Random().Next().ToString();
            clipboard.SetText(text);
            Assert.AreEqual(text, clipboard.GetText(Universal.Interfaces.TextDataFormat.UnicodeText));
        }
    }
}
