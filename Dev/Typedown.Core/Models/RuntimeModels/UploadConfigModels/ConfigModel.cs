using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Typedown.Core.Models.UploadConfigModels
{
    public partial class ConfigModel : INotifyPropertyChanged
    {
        public Dictionary<string, JToken> Addition { get; } = new();

        public string UploadPath { get; set; } = string.Empty;

        public string ExternalURL { get; set; } = string.Empty;

        public virtual Task<string> Upload(IServiceProvider serviceProvider, string filePath) => Task.FromResult(filePath);
    }
}
