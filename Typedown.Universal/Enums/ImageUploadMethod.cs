using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum ImageUploadMethod
    {
        [Locale("None")]
        None = 0,

        [Locale("ImageUploadMethod/FTP")]
        FTP = 1,

        [Locale("ImageUploadMethod/Git")]
        Git = 2,

        [Locale("ImageUploadMethod/OSS")]
        OSS = 3,

        [Locale("ImageUploadMethod/SCP")]
        SCP = 4,

        [Locale("ImageUploadMethod/PowerShell")]
        PowerShell = 1024
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ImageUploadMethod> ImageUploadMethods { get; } = Enum.GetValues(typeof(ImageUploadMethod)).Cast<ImageUploadMethod>().ToList();

        public static IReadOnlyList<ImageUploadMethod> AvailableImageUploadMethods { get; } = new List<ImageUploadMethod>() { ImageUploadMethod.PowerShell };
    }
}
