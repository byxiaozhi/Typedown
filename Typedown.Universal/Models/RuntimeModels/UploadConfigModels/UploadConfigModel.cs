using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models.UploadConfigModels
{
    public partial class UploadConfigModel : INotifyPropertyChanged
    {
        public Dictionary<string, JToken> Addition { get; } = new();

        public string UploadPath { get; set; } = string.Empty;

        public string ExternalURL { get; set; } = string.Empty;
    }
}
