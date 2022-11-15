using Newtonsoft.Json.Linq;
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

        public T LoadUploadConfig<T>() where T : UploadConfigModel, new()
        {
            var config = ParseConfig();
            return config.TryGetValue(typeof(T).Name, out var value) ? value.ToObject<T>() : new();
        }

        public void StoreUploadConfig<T>(T uploadConfig) where T : UploadConfigModel
        {
            var config = ParseConfig();
            config[typeof(T).Name] = JObject.FromObject(uploadConfig);
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
