using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>The application and the automation test host stay apart in the sources, not only by convention.</summary>
    public class BuildSeparationTests
    {
        private static readonly string Dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

        [Fact]
        public void No_application_project_references_the_test_host()
        {
            var projects = Directory.GetFiles(Dev, "*.csproj", SearchOption.AllDirectories)
                .Where(p => !p.Contains("Typedown.Automation.Tests") && !p.Contains("Typedown.Automation.TestHost") && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .ToList();
            Assert.Contains(projects, p => p.EndsWith("Typedown.Core.csproj"));
            foreach (var project in projects)
            {
                var xml = System.Xml.Linq.XDocument.Load(project);
                foreach (var element in xml.Descendants().Where(e => ((string?)e.Attribute("Include") ?? "").Contains("Automation.TestHost")))
                {
                    // Only the test host variant of the application may pull it in.
                    var condition = (string?)element.Parent?.Attribute("Condition") ?? (string?)element.Attribute("Condition") ?? "";
                    Assert.True(condition.Replace(" ", "") == "'$(AutomationTestHost)'=='true'",
                        $"{Path.GetFileName(project)} references the test host outside the AutomationTestHost build");
                }
            }
        }

        [Fact]
        public void Only_the_test_host_variant_of_the_app_references_it()
        {
            var app = File.ReadAllText(Path.Combine(Dev, "Typedown", "Typedown.csproj"));
            Assert.Contains("Typedown.Automation.TestHost.csproj", app);
            Assert.DoesNotContain("Automation.TestHost", File.ReadAllText(Path.Combine(Dev, "Typedown.Core", "Typedown.Core.csproj")));
        }

        [Fact]
        public void The_shared_library_names_no_test_method()
        {
            foreach (var file in Directory.GetFiles(Path.Combine(Dev, "Typedown.Automation"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/")))
                Assert.DoesNotContain("\"test.barrier", File.ReadAllText(file));
        }
    }
}
