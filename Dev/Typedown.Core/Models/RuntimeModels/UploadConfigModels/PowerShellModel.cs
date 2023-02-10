using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;

namespace Typedown.Core.Models.UploadConfigModels
{
    public class PowerShellModel : ConfigModel
    {
        public static string DefaultScript = "# Upload file and return URL\nfunction Upload-Image($FilePath)\n{    \n    return $FilePath\n}";

        public string Script { get; set; } = DefaultScript;

        public override async Task<string> Upload(IServiceProvider serviceProvider, string filePath)
        {
            return await Task.Run(() =>
            {
                var powerShell = serviceProvider.GetService<IPowerShellService>();
                var result = powerShell.Invoke(Script, "Upload-Image", filePath);
                return result.First();
            });
        }
    }
}
