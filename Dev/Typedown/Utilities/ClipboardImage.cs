using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Typedown.Utilities
{
    public class ClipboardImage : IClipboardImage
    {
        private readonly RandomAccessStreamReference bitmapStreamRef;

        public ClipboardImage(RandomAccessStreamReference bitmapStreamRef)
        {
            this.bitmapStreamRef = bitmapStreamRef;
        }

        public byte[] GetBytes()
        {
            return GetBytesAsync().GetAwaiter().GetResult();
        }

        private async Task<byte[]> GetBytesAsync()
        {
            using var stream = await bitmapStreamRef.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync();
            var bytes = pixelData.DetachPixelData();

            using var memoryStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memoryStream);
            encoder.SetPixelData(
                decoder.BitmapPixelFormat,
                decoder.BitmapAlphaMode,
                decoder.PixelWidth,
                decoder.PixelHeight,
                decoder.DpiX,
                decoder.DpiY,
                bytes);
            await encoder.FlushAsync();

            var result = new byte[memoryStream.Size];
            await memoryStream.AsStream().ReadAsync(result, 0, result.Length);
            return result;
        }

        public void SaveAsPng(string path)
        {
            SaveAsPngAsync(path).GetAwaiter().GetResult();
        }

        private async Task SaveAsPngAsync(string path)
        {
            var bytes = await GetBytesAsync();
            await File.WriteAllBytesAsync(path, bytes);
        }
    }
}