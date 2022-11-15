using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models.ExportConfigModels
{
    public class HTMLConfigModel : ConfigModel
    {
        public string HeadExtra { get; set; }

        public string BodyExtra { get; set; }
    }
}
