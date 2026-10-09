using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Typedown.Core.Utilities;

namespace Typedown.ReliabilityTests
{
    [TestClass]
    public class ManifestAndArchitectureTests
    {
        [TestMethod]
        public void PackageKeepsExpectedFileAssociationsAndFullTrustCapability()
        {
            var manifest = XDocument.Load(Path.Combine(Repository.Root, "Tools", "Typedown.Package", "Package.appxmanifest"));
            XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
            XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
            var expected = new[] { ".markdown", ".md", ".mdown", ".mdtext", ".mdtxt", ".mdwn", ".mkd", ".mkdn", ".rmd", ".text", ".txt" };
            var actual = manifest.Descendants(uap + "FileType").Select(node => node.Value.Trim()).OrderBy(value => value).ToArray();

            CollectionAssert.AreEqual(expected.OrderBy(value => value).ToArray(), actual);
            Assert.IsNotNull(manifest.Descendants(rescap + "Capability").SingleOrDefault(node => (string)node.Attribute("Name") == "runFullTrust"));
            Assert.IsNotNull(manifest.Descendants(foundation + "Application").SingleOrDefault(node => (string)node.Attribute("Id") == "App"));
            Assert.IsNotNull(manifest.Descendants(foundation + "Application").SingleOrDefault(node => (string)node.Attribute("Id") == "Cli"));
        }

        [TestMethod]
        public void PackageAndApplicationVersionsStayAligned()
        {
            var application = XDocument.Load(Path.Combine(Repository.Root, "Dev", "Typedown", "Typedown.csproj"));
            var appVersion = application.Descendants().Single(node => node.Name.LocalName == "Version").Value.Trim();
            var manifest = XDocument.Load(Path.Combine(Repository.Root, "Tools", "Typedown.Package", "Package.appxmanifest"));
            var packageVersion = manifest.Descendants().Single(node => node.Name.LocalName == "Identity").Attribute("Version")?.Value;
            var assembly = File.ReadAllText(Path.Combine(Repository.Root, "Dev", "Typedown.Core", "Properties", "AssemblyInfo.cs"));
            var installer = File.ReadAllText(Path.Combine(Repository.Root, "Tools", "Installer", "Typedown.iss"));

            Assert.AreEqual(appVersion + ".0", packageVersion);
            StringAssert.Contains(assembly, $"AssemblyVersion(\"{appVersion}.0\")");
            StringAssert.Contains(assembly, $"AssemblyFileVersion(\"{appVersion}.0\")");
            StringAssert.Contains(installer, $"#define MyAppVersion \"{appVersion}\"");
        }

        [TestMethod]
        public void StandaloneThemeDesignerIsCopiedIntoApplicationOutput()
        {
            var project = XDocument.Load(Path.Combine(Repository.Root, "Dev", "Typedown", "Typedown.csproj"));
            var item = project.Descendants().SingleOrDefault(node => node.Name.LocalName == "None" &&
                (((string)node.Attribute("Include")) ?? string.Empty).Replace('/', '\\').EndsWith("Tools\\Themes\\theme-designer.html", StringComparison.OrdinalIgnoreCase));

            Assert.IsNotNull(item, "theme-designer.html is not included by the desktop host project");
            Assert.AreEqual("Resources\\Themes\\theme-designer.html", item.Attributes().Single(attribute => attribute.Name.LocalName == "Link").Value);
            Assert.AreEqual("PreserveNewest", item.Attributes().Single(attribute => attribute.Name.LocalName == "CopyToOutputDirectory").Value);
            Assert.AreEqual("PreserveNewest", item.Attributes().Single(attribute => attribute.Name.LocalName == "CopyToPublishDirectory").Value);

            var html = File.ReadAllText(Path.Combine(Repository.Root, "Tools", "Themes", "theme-designer.html"));
            Assert.AreEqual(1, Regex.Matches(html, Regex.Escape(ThemeDesignerPage.EmptyCatalog)).Count);
        }

        [TestMethod]
        public void SupportedLanguagesMatchResourcesAndPackageManifest()
        {
            var source = File.ReadAllText(Path.Combine(Repository.Root, "Dev", "Typedown.Core", "Utilities", "Locale.cs"));
            var supportedBlock = source.Substring(source.IndexOf("SupportedLangs", StringComparison.Ordinal),
                source.IndexOf("CommonLangs", StringComparison.Ordinal) - source.IndexOf("SupportedLangs", StringComparison.Ordinal));
            var supported = Regex.Matches(supportedBlock, "\\{\\\"([^\\\"]+)\\\",")
                .Select(match => match.Groups[1].Value).OrderBy(value => value).ToArray();
            var resources = Directory.GetDirectories(Path.Combine(Repository.Root, "Dev", "Typedown.Core", "Resources", "Strings"))
                .Select(Path.GetFileName).OrderBy(value => value).ToArray();
            var manifest = XDocument.Load(Path.Combine(Repository.Root, "Tools", "Typedown.Package", "Package.appxmanifest"));
            var packaged = manifest.Descendants()
                .Where(node => node.Name.LocalName == "Resource")
                .Select(node => (string)node.Attribute("Language"))
                .Where(value => !string.IsNullOrEmpty(value)).OrderBy(value => value).ToArray();

            CollectionAssert.AreEqual(resources, supported, "Locale.SupportedLangs and resource directories differ");
            CollectionAssert.AreEqual(resources, packaged, "resource directories and package manifest differ");
        }

        [TestMethod]
        public void CoreNeverReferencesTheDesktopHost()
        {
            var core = XDocument.Load(Path.Combine(Repository.Root, "Dev", "Typedown.Core", "Typedown.Core.csproj"));
            var references = core.Descendants()
                .Where(node => node.Name.LocalName == "ProjectReference")
                .Select(node => ((string)node.Attribute("Include") ?? string.Empty).Replace('/', '\\'))
                .ToArray();

            Assert.IsFalse(references.Any(reference => reference.Contains("\\Typedown\\Typedown.csproj", StringComparison.OrdinalIgnoreCase)),
                "Typedown.Core must not depend on the desktop host project");
        }

        [TestMethod]
        public void DesktopHostReferencesCoreInOneDirection()
        {
            var host = XDocument.Load(Path.Combine(Repository.Root, "Dev", "Typedown", "Typedown.csproj"));
            var references = host.Descendants()
                .Where(node => node.Name.LocalName == "ProjectReference")
                .Select(node => ((string)node.Attribute("Include") ?? string.Empty).Replace('/', '\\'))
                .ToArray();

            Assert.IsTrue(references.Any(reference => reference.EndsWith("\\Typedown.Core\\Typedown.Core.csproj", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
