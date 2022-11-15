using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Typedown.Universal.Models.ExportConfigModels;

namespace Typedown.Universal.Models
{
    [Table("ExportConfig")]
    public partial class ExportConfig : INotifyPropertyChanged
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public ExportType Type { get; set; }

        public string Config { get; private set; } = new JObject().ToString();

        public T LoadExportConfig<T>() where T : ConfigModel, new()
        {
            var config = ParseConfig();
            return config.TryGetValue(typeof(T).Name, out var value) ? value.ToObject<T>() : new();
        }

        public void StoreExportConfig<T>(T exportConfig) where T : ConfigModel
        {
            var config = ParseConfig();
            config[typeof(T).Name] = JObject.FromObject(exportConfig);
            Config = config.ToString();
        }

        private JObject ParseConfig()
        {
            try
            {
                return JObject.Parse(Config);
            }
            catch
            {
                return new();
            }
        }
    }
}
