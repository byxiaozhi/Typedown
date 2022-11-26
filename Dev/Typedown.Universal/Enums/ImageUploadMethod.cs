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

        [Locale("ImageUpload.Methods.FTP")]
        FTP = 1,

        [Locale("ImageUpload.Methods.SCP")]
        Git = 2,

        [Locale("ImageUpload.Methods.OSS")]
        OSS = 3,

        [Locale("ImageUpload.Methods.SCP")]
        SCP = 4,

        [Locale("ImageUpload.Methods.PowerShell")]
        PowerShell = 1024
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ImageUploadMethod> ImageUploadMethods { get; } = Enum.GetValues(typeof(ImageUploadMethod)).Cast<ImageUploadMethod>().ToList();

        public static IReadOnlyList<ImageUploadMethod> AvailableImageUploadMethods { get; } = new List<ImageUploadMethod>() { ImageUploadMethod.PowerShell };
    }
}
