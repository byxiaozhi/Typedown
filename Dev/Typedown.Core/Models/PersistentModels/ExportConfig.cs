using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Typedown.Core.Enums;
using Typedown.Core.Models.ExportConfigModels;

namespace Typedown.Core.Models
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

        public List<(string name, string extension)> FileExtensions => GetFileExtensions();

        public string Config { get; private set; } = new JObject().ToString();

        public ConfigModel LoadExportConfig()
        {
            try
            {
                var allConfig = ParseConfig();
                if (allConfig.TryGetValue(GetConfigModelKey(), out var value) && value.ToObject(GetConfigModelType()) is ConfigModel config)
                    return config;
            }
            catch
            {
                // Return default value;
            }
            var defaultConfig = GetDefaultConfigModel();
            StoreExportConfig(defaultConfig);
            return defaultConfig;
        }

        public void StoreExportConfig(ConfigModel exportConfig)
        {
            var config = ParseConfig();
            config[GetConfigModelKey()] = JObject.FromObject(exportConfig);
            Config = config.ToString();
        }

        private Type GetConfigModelType()
        {
            return Type switch
            {
                ExportType.PDF => typeof(PDFConfigModel),
                ExportType.HTML => typeof(HTMLConfigModel),
                ExportType.Image => typeof(ImageConfigModel),
                _ => typeof(ConfigModel)
            };
        }

        private List<(string, string)> GetFileExtensions()
        {
            return Type switch
            {
                ExportType.PDF => new() { ("PDF", ".pdf") },
                ExportType.HTML => new() { ("HTML", ".html") },
                ExportType.Image => new() { ("PNG", ".png") },
                _ => new()
            };
        }

        private string GetConfigModelKey()
        {
            return Type switch
            {
                ExportType.PDF => "PDF",
                ExportType.HTML => "HTML",
                ExportType.Image => "Image",
                _ => null
            };
        }

        private ConfigModel GetDefaultConfigModel()
        {
            return Type switch
            {
                ExportType.PDF => new PDFConfigModel(),
                ExportType.HTML => new HTMLConfigModel(),
                ExportType.Image => new ImageConfigModel(),
                _ => null
            };
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
