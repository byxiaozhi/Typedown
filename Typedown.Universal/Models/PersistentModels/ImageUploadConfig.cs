using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Typedown.Universal.Enums;
using Typedown.Universal.Models.UploadConfigModels;

namespace Typedown.Universal.Models
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
            var config = ParseConfig();
            if (config.TryGetValue(((int)Method).ToString(), out var value))
                return value.ToObject(GetConfigModelType()) as ConfigModel;
            return null;
        }

        public T LoadUploadConfig<T>() where T: ConfigModel, new()
        {
            return (LoadUploadConfig() as T) ?? new();
        }

        public void StoreUploadConfig(ConfigModel uploadConfig)
        {
            var config = ParseConfig();
            config[((int)Method).ToString()] = JObject.FromObject(uploadConfig);
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
