using System;
using System.IO;
using System.Threading.Tasks;

namespace Typedown.Core.Models.ExportConfigModels
{
    public class HTMLConfigModel : ConfigModel
    {
        public string ExtraHead { get; set; } = "";

        public string ExtraBody { get; set; } = "";

        public override async Task Export(IServiceProvider serviceProvider, string html, string filePath)
        {
            await File.WriteAllTextAsync(filePath, html);
        }
    }
}
