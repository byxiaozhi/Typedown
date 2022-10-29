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
        FTP,

        [Localize("ImageUploadMethod/Git")]
        Git,

        [Localize("ImageUploadMethod/OSS")]
        OSS,

        [Localize("ImageUploadMethod/SSH")]
        SSH,

        [Localize("ImageUploadMethod/CMD")]
        CMD = 1024
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ImageUploadMethod> ImageUploadMethods { get; } = Enum.GetValues(typeof(ImageUploadMethod)).Cast<ImageUploadMethod>().ToList();
    }
}
