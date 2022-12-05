using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Core.Models.UploadConfigModels
{
    public class GitConfigModel : ConfigModel
    {
        public string URL { get; set; } = string.Empty;

        public string IdentityFile { get; set; } = string.Empty;

        public string Passphrase { get; set; } = string.Empty;
    }
}
