using System;

namespace Typedown.WinUI.Services.Export
{
    // What Word needs to know about a picture: its format (from the first bytes, never from the name) and its size in pixels.
    // Read straight from the file header, so no drawing library is needed. Plain .NET (no WinUI).
    internal sealed record ImageInfo(string ContentType, int Width, int Height)
    {
        // null: not a picture Word can show (SVG, WebP, a text file named .png) or a header that can not be read.
        public static ImageInfo Read(byte[] data)
        {
            if (data == null || data.Length < 24) return null;
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return Valid("image/png", ReadInt32BigEndian(data, 16), ReadInt32BigEndian(data, 20));
            if (data[0] == 0xFF && data[1] == 0xD8) return Jpeg(data);
            if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
                return Valid("image/gif", data[6] | (data[7] << 8), data[8] | (data[9] << 8));
            if (data[0] == 'B' && data[1] == 'M' && data.Length >= 26) // the height is read at offset 22
            {
                var width = BitConverter.ToInt32(data, 18);
                var rawHeight = BitConverter.ToInt32(data, 22);
                if (rawHeight == int.MinValue) return null; // Math.Abs would throw on it
                var height = Math.Abs(rawHeight); // a negative height means the rows are stored top down
                return Valid("image/bmp", width, height);
            }
            if ((data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0) || (data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42))
                return Tiff(data);
            return null;
        }

        private static ImageInfo Valid(string contentType, int width, int height) =>
            width > 0 && height > 0 && width < 100000 && height < 100000 ? new ImageInfo(contentType, width, height) : null;

        private static int ReadInt32BigEndian(byte[] d, int at) => (d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3];

        // The size is in the start-of-frame marker; the segments before it are skipped by their lengths.
        private static ImageInfo Jpeg(byte[] d)
        {
            var i = 2;
            while (i + 9 < d.Length)
            {
                if (d[i] != 0xFF) { i++; continue; }
                var marker = d[i + 1];
                if (marker == 0xFF) { i++; continue; }
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                var length = (d[i + 2] << 8) | d[i + 3];
                var isFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isFrame) return Valid("image/jpeg", (d[i + 7] << 8) | d[i + 8], (d[i + 5] << 8) | d[i + 6]);
                if (length < 2) return null;
                i += 2 + length;
            }
            return null;
        }

        private static ImageInfo Tiff(byte[] d)
        {
            var little = d[0] == 'I';
            int Short(int at) => at + 2 > d.Length ? 0 : little ? d[at] | (d[at + 1] << 8) : (d[at] << 8) | d[at + 1];
            int Long(int at) => at + 4 > d.Length ? 0 : little ? d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24) : (d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3];
            var directory = Long(4);
            if (directory < 8 || directory + 2 > d.Length) return null;
            int width = 0, height = 0;
            var count = Short(directory);
            for (var n = 0; n < count && directory + 2 + (n + 1) * 12 <= d.Length; n++)
            {
                var entry = directory + 2 + n * 12;
                var tag = Short(entry);
                var type = Short(entry + 2);
                var value = type == 3 ? Short(entry + 8) : Long(entry + 8);
                if (tag == 256) width = value;
                else if (tag == 257) height = value;
            }
            return Valid("image/tiff", width, height);
        }
    }
}
