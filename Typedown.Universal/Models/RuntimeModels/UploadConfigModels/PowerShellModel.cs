using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;

namespace Typedown.Universal.Models.UploadConfigModels
{
    public class PowerShellModel : ConfigModel
    {
        public static string DefaultScript = "# Upload file and return URL\nfunction Upload-Image($FilePath)\n{    \n    return $FilePath\n}";

        public string Script { get; set; } = DefaultScript;

        public override async Task<string> Upload(IServiceProvider serviceProvider, string filePath)
        {
            return await Task.Run(() =>
            {
                using var powerShell = System.Management.Automation.PowerShell.Create();
                powerShell.AddScript(Script);
                powerShell.Invoke();
                powerShell.Commands.Clear();
                powerShell.AddCommand("Upload-Image").AddParameters(new List<string>() { filePath });
                var result = powerShell.Invoke();
                return result.First().ToString();
            });
        }
    }
}
