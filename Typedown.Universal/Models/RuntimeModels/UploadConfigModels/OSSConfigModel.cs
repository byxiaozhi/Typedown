using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models.UploadConfigModels
{
    public class OSSConfigModel : UploadConfigModel
    {
        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string ServiceURL { get; set; } = string.Empty;

        public string RegionEndpoint { get; set; } = string.Empty;
    }
}
