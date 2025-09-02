using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
        public async Task SetTextAndGetText_WithRandomText_CanSetAndGet()
        {
            var clipboard = GetDefaultClipboardService();
            var text = new Random().Next().ToString();
            clipboard.SetText(text);
            Assert.AreEqual(text, await clipboard.GetTextAsync(Core.Interfaces.TextDataFormat.UnicodeText));
        }
    }
}
