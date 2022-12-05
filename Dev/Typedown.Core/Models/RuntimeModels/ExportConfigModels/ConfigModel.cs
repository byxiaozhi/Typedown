using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Core.Models.ExportConfigModels
{
    public partial class ConfigModel : INotifyPropertyChanged
    {
        public Dictionary<string, JToken> Addition { get; } = new();

        public string ScriptAfter { get; } = string.Empty;

        public virtual Task Export(IServiceProvider serviceProvider, string html, string filePath) => null;
    }
}
