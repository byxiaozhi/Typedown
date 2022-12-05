using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Typedown.Core.Enums;
using Typedown.Core.Models.UploadConfigModels;

namespace Typedown.Core.Models
{
    [Table("ImageUploadConfig")]
    public partial class ImageUploadConfig : INotifyPropertyChanged
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public bool IsEnable { get; set; }

        public ImageUploadMethod Method { get; set; }

        public string Config { get; private set; } = new JObject().ToString();

        public ConfigModel LoadUploadConfig()
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
            StoreUploadConfig(defaultConfig);
            return defaultConfig;
        }

        public void StoreUploadConfig(ConfigModel uploadConfig)
        {
            var config = ParseConfig();
            config[GetConfigModelKey()] = JObject.FromObject(uploadConfig);
            Config = config.ToString();
        }

        private Type GetConfigModelType()
        {
            return Method switch
            {
                ImageUploadMethod.FTP => typeof(FTPConfigModel),
                ImageUploadMethod.Git => typeof(GitConfigModel),
                ImageUploadMethod.OSS => typeof(OSSConfigModel),
                ImageUploadMethod.SCP => typeof(SCPConfigModel),
                ImageUploadMethod.PowerShell => typeof(PowerShellModel),
                _ => typeof(ConfigModel)
            };
        }

        private string GetConfigModelKey()
        {
            return Method switch
            {
                ImageUploadMethod.FTP => "FTP",
                ImageUploadMethod.Git => "Git",
                ImageUploadMethod.OSS => "OSS",
                ImageUploadMethod.SCP => "SCP",
                ImageUploadMethod.PowerShell => "PowerShell",
                _ => null
            };
        }

        private ConfigModel GetDefaultConfigModel()
        {
            return Method switch
            {
                ImageUploadMethod.FTP => new FTPConfigModel(),
                ImageUploadMethod.Git => new GitConfigModel(),
                ImageUploadMethod.OSS => new OSSConfigModel(),
                ImageUploadMethod.SCP => new SCPConfigModel(),
                ImageUploadMethod.PowerShell => new PowerShellModel(),
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
