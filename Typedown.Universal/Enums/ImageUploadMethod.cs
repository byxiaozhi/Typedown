using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum ImageUploadMethod
    {
        [Localize("None")]
        None = 0,

        [Localize("ImageUploadMethod/FTP")]
        FTP = 1,

        [Localize("ImageUploadMethod/Git")]
        Git = 2,

        [Localize("ImageUploadMethod/OSS")]
        OSS = 3,

        [Localize("ImageUploadMethod/SCP")]
        SCP = 4,

        [Localize("ImageUploadMethod/PowerShell")]
        PowerShell = 1024
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ImageUploadMethod> ImageUploadMethods { get; } = Enum.GetValues(typeof(ImageUploadMethod)).Cast<ImageUploadMethod>().ToList();
    }
}
