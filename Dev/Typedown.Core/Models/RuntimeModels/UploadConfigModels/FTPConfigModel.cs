namespace Typedown.Core.Models.UploadConfigModels
{
    public class FTPConfigModel : ConfigModel
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 21;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
