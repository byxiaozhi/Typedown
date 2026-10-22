using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Typedown.WinUI.Utilities;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // Pictures: read from the file next to the Markdown (or the data: address), put in the .docx, and scaled to the text column.
    // Nothing is fetched from the network.
    internal sealed partial class WordBuilder
    {
        private const long EmuPerPixel = 9525; // 96 dpi
        private const long EmuPerTwip = 635;
        private const long LargestPicture = 100L * 1024 * 1024;

        private int pictures;
        private uint drawingId;
        private readonly Dictionary<(OpenXmlPartContainer Part, string Source), (string Id, ImageInfo Info)> loaded = new();

        // null when the picture cannot be put in; the address is then in the list of skipped pictures.
        private W.Run PictureRun(string source, string alt, double? zoomPercent, int? widthPixels, int? heightPixels, string skipName = null)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;
            if (!loaded.TryGetValue((currentPart, source), out var part))
            {
                var data = Load(source);
                var info = data == null ? null : ImageInfo.Read(data);
                if (info == null)
                {
                    skipped.Add(skipName ?? (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? "data:..." : source));
                    return null;
                }
                var imagePart = currentPart is FootnotesPart notes ? notes.AddImagePart(info.ContentType) : main.AddImagePart(info.ContentType);
                using (var stream = new MemoryStream(data)) imagePart.FeedData(stream);
                loaded[(currentPart, source)] = part = (currentPart.GetIdOfPart(imagePart), info);
            }
            double width = part.Info.Width, height = part.Info.Height;
            if (widthPixels is > 0) { height = height * widthPixels.Value / width; width = widthPixels.Value; }
            else if (heightPixels is > 0) { width = width * heightPixels.Value / height; height = heightPixels.Value; }
            if (zoomPercent is > 0) { width *= zoomPercent.Value / 100; height *= zoomPercent.Value / 100; }
            var limit = (double)textWidth * EmuPerTwip / EmuPerPixel;
            if (width > limit) { height = height * limit / width; width = limit; }
            pictures++;
            return new W.Run(Drawing(part.Id, Math.Max(1, (long)(width * EmuPerPixel)), Math.Max(1, (long)(height * EmuPerPixel)), alt ?? ""));
        }

        // What is in the text where there is no picture: the words of a picture that could not be loaded.
        private W.Run PictureFallback(string source, string alt, Fmt fmt)
        {
            var text = string.IsNullOrWhiteSpace(alt) ? Path.GetFileName((source ?? "").Replace("\\", "/")) : alt;
            return TextRun("[" + text + "]", fmt);
        }

        private W.Drawing Drawing(string relationshipId, long cx, long cy, string alt)
        {
            var id = ++drawingId;
            return new W.Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = id, Name = "Picture " + id, Description = alt },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0, Name = "Picture " + id, Description = alt },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        }

        private byte[] Load(string source)
        {
            try
            {
                if (diagramPictures.TryGetValue(source, out var diagram)) return diagram;
                if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = source.IndexOf(",", StringComparison.Ordinal);
                    if (comma < 0) return null;
                    var payload = source.Substring(comma + 1);
                    return source.Substring(0, comma).Contains(";base64", StringComparison.OrdinalIgnoreCase)
                        ? Convert.FromBase64String(payload) : Encoding.Latin1.GetBytes(Uri.UnescapeDataString(payload));
                }
                if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
                var path = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(source).LocalPath : Uri.UnescapeDataString(source);
                if (!Path.IsPathRooted(path))
                {
                    if (string.IsNullOrEmpty(options.BaseFolder)) return null;
                    path = Path.Combine(options.BaseFolder, path);
                }
                var file = new FileInfo(path);
                return file.Exists && file.Length <= LargestPicture ? File.ReadAllBytes(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or UriFormatException or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        private static readonly Regex Attribute = new(@"([a-zA-Z-]+)\s*=\s*(?:""([^""]*)""|([^""\s>]+))");

        // A picture written as HTML, the way the editor writes one that has a size: src, alt, style="zoom:50%" or width.
        private OpenXmlElement HtmlPicture(string tag)
        {
            string src = null, alt = null, style = null, width = null, height = null;
            foreach (Match m in Attribute.Matches(tag))
            {
                var value = WebUtility.HtmlDecode(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
                switch (m.Groups[1].Value.ToLowerInvariant())
                {
                    case "src": src = value; break;
                    case "alt": alt = value; break;
                    case "style": style = value; break;
                    case "width": width = value; break;
                    case "height": height = value; break;
                }
            }
            if (src == null) return null;
            int? w = int.TryParse(width, out var wi) ? wi : null, h = int.TryParse(height, out var hi) ? hi : null;
            return PictureRun(src, alt, ImageStyle.Zoom(style), w, h) ?? PictureFallback(src, alt, new Fmt());
        }
    }
}
