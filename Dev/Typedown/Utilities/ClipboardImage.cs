using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Typedown.Core.Interfaces;

namespace Typedown.Utilities
{
    public class ClipboardImage : IClipboardImage
    {
        private readonly System.Drawing.Image image;

        public ClipboardImage(System.Drawing.Image image)
        {
            this.image = image;
        }

        public byte[] GetBytes()
        {
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        public void SaveAsPng(string path)
        {
            image.Save(path);
        }
    }
}
