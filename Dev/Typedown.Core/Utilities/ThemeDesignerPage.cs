using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace Typedown.Core.Utilities
{
    /// <summary>A theme made available to the standalone designer when Typedown opens it.</summary>
    public sealed class ThemeDesignerEntry
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string FileName { get; set; }

        public string Source { get; set; }

        public string Css { get; set; }
    }

    public sealed class ThemeDesignerCatalog
    {
        public int Version { get; set; } = 1;

        public string SelectedId { get; set; }

        public IReadOnlyList<ThemeDesignerEntry> Themes { get; set; } = Array.Empty<ThemeDesignerEntry>();
    }

    /// <summary>
    /// Adds a data-only snapshot to the packaged standalone HTML. A browser deliberately cannot enumerate an
    /// arbitrary local folder from a file:// page, so Typedown reads the folder and embeds its current contents
    /// before opening the page. EscapeHtml prevents a theme containing &lt;/script&gt; from breaking out of the
    /// application/json element; the designer reads the result with JSON.parse and only renders textContent.
    /// </summary>
    public static class ThemeDesignerPage
    {
        public const string EmptyCatalog = "<script id=\"typedown-theme-catalog\" type=\"application/json\">[]</script>";

        public static string EmbedCatalog(string template, ThemeDesignerCatalog catalog)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var first = template.IndexOf(EmptyCatalog, StringComparison.Ordinal);
            if (first < 0 || template.IndexOf(EmptyCatalog, first + EmptyCatalog.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("theme designer catalog placeholder is missing or duplicated");

            var json = JsonConvert.SerializeObject(catalog, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
            var embedded = $"<script id=\"typedown-theme-catalog\" type=\"application/json\">{json}</script>";
            return template.Substring(0, first) + embedded + template.Substring(first + EmptyCatalog.Length);
        }
    }
}
