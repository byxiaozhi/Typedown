using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models.UploadConfigModels
{
    public class SCPConfigModel : ConfigModel
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 22;

        public string Username { get; set; } = string.Empty;

        public bool PubKeyAuthentication { get; set; }

        public string Password { get; set; } = string.Empty;

        public string IdentityFile { get; set; } = string.Empty;

        public string Passphrase { get; set; } = string.Empty;
    }
}
