using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Typedown.Core.Utilities;

namespace Typedown.ReliabilityTests
{
    [TestClass]
    public class ThemeDesignerPageTests
    {
        [TestMethod]
        public void CatalogIsEmbeddedAsDataWithoutAllowingScriptBreakout()
        {
            var dangerousCss = "body::after { content: '</script><script>bad()</script>'; }";
            var catalog = new ThemeDesignerCatalog
            {
                SelectedId = "danger",
                Themes = new[]
                {
                    new ThemeDesignerEntry { Id = "danger", Name = "A <theme>", FileName = "danger.css", Source = "user", Css = dangerousCss }
                }
            };

            var html = ThemeDesignerPage.EmbedCatalog("before" + ThemeDesignerPage.EmptyCatalog + "after", catalog);
            Assert.IsFalse(html.Contains(dangerousCss, StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("<script>bad()", StringComparison.Ordinal));

            var match = Regex.Match(html, "<script id=\\\"typedown-theme-catalog\\\" type=\\\"application/json\\\">(.*?)</script>", RegexOptions.Singleline);
            Assert.IsTrue(match.Success);
            var decoded = JsonConvert.DeserializeObject<ThemeDesignerCatalog>(match.Groups[1].Value);
            Assert.AreEqual("danger", decoded.SelectedId);
            Assert.AreEqual(dangerousCss, decoded.Themes.Single().Css);
        }

        [TestMethod]
        public void CatalogPlaceholderMustOccurExactlyOnce()
        {
            var catalog = new ThemeDesignerCatalog();
            Assert.ThrowsException<InvalidDataException>(() => ThemeDesignerPage.EmbedCatalog("missing", catalog));
            Assert.ThrowsException<InvalidDataException>(() => ThemeDesignerPage.EmbedCatalog(ThemeDesignerPage.EmptyCatalog + ThemeDesignerPage.EmptyCatalog, catalog));
        }
    }
}
