using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.ComponentModel;
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
