using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models.UploadConfigModels
{
    public class PowerShellModel : UploadConfigModel
    {
        public string Script { get; set; } = string.Empty;
    }
}
