using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Models.ExportConfigModels
{
    public class PDFConfigModel : ConfigModel
    {
        public string ExtraHead { get; set; } = "<style>.markdown-body { min-width: unset; max-width: unset; margin: unset; padding: 0; }</style>";

        public PrintOrientation Orientation { get; set; }

        public PageSize PageSize { get; } = new();

        public PageMargin Margins { get; } = new();

        public bool ShouldPrintHeaderAndFooter { get; set; }

        public string Header { get; set; } = string.Empty;

        public string Footer { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public override async Task Export(IServiceProvider serviceProvider, string html, string filePath)
        {
            var converter = serviceProvider.GetService<IFileConverter>();
            var settings = new PdfPrintSettings()
            {
                Orientation = Orientation,
                PageSize = new(PageSize.Width.GetValue(Units.Inch), PageSize.Height.GetValue(Units.Inch)),
                Margin = new(Margins.Left.GetValue(Units.Inch), Margins.Top.GetValue(Units.Inch), Margins.Right.GetValue(Units.Inch), Margins.Bottom.GetValue(Units.Inch)),
                ShouldPrintHeaderAndFooter = ShouldPrintHeaderAndFooter,
                Header = Header,
                Footer = Footer
            };
            var pdf = await converter.HtmlToPdf(html, settings);
            await File.WriteAllBytesAsync(filePath, pdf.ToArray());
        }
    }

    public partial class PageSize : INotifyPropertyChanged
    {
        public DimNumber Width { get; set; } = new(Units.Centimeter, 21);

        public DimNumber Height { get; set; } = new(Units.Centimeter, 29.7);

        public PageSize()
        {
            
        }

        public PageSize(DimNumber width, DimNumber height)
        {
            Width = width;
            Height = height;
        }

        public bool ApproxEquals(PageSize pageSize)
        {
            return Math.Abs(pageSize.Width.GetValue(Units.Centimeter) - Width.GetValue(Units.Centimeter)) < 1e-3 &&
                    Math.Abs(pageSize.Height.GetValue(Units.Centimeter) - Height.GetValue(Units.Centimeter)) < 1e-3;
        }

        public static IReadOnlyList<(string Name, PageSize PageSize)> StandardPageSizes => new List<(string, PageSize)>()
        {
            ("Letter", new() { Width = new(Units.Inch, 8.5), Height = new(Units.Inch, 11) }),
            ("Tabloid", new() { Width = new(Units.Inch, 11), Height = new(Units.Inch, 17) }),
            ("Legal", new() { Width = new(Units.Inch, 8.5), Height = new(Units.Inch, 14) }),
            ("A3", new() { Width = new(Units.Centimeter, 29.7), Height = new(Units.Centimeter, 42) }),
            ("A4", new() { Width = new(Units.Centimeter, 21), Height = new(Units.Centimeter, 29.7) }),
            ("A5", new() { Width = new(Units.Centimeter, 14.8), Height = new(Units.Centimeter, 21) })
        };
    }

    public partial class PageMargin : INotifyPropertyChanged
    {
        public DimNumber Left { get; set; } = new(Units.Centimeter, 3.18);

        public DimNumber Top { get; set; } = new(Units.Centimeter, 2.54);

        public DimNumber Right { get; set; } = new(Units.Centimeter, 3.18);

        public DimNumber Bottom { get; set; } = new(Units.Centimeter, 2.54);

        public PageMargin()
        {

        }

        public PageMargin(DimNumber left, DimNumber top, DimNumber right, DimNumber bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool ApproxEquals(PageMargin pageMargin)
        {
            return Math.Abs(pageMargin.Left.GetValue(Units.Centimeter) - Left.GetValue(Units.Centimeter)) < 1e-3 &&
                    Math.Abs(pageMargin.Top.GetValue(Units.Centimeter) - Top.GetValue(Units.Centimeter)) < 1e-3 &&
                    Math.Abs(pageMargin.Right.GetValue(Units.Centimeter) - Right.GetValue(Units.Centimeter)) < 1e-3 &&
                    Math.Abs(pageMargin.Bottom.GetValue(Units.Centimeter) - Bottom.GetValue(Units.Centimeter)) < 1e-3;
        }

        public static IReadOnlyList<(string Name, PageMargin PageMargin)> StandardPageMargin => new List<(string, PageMargin)>()
        {
            ("Normal", new() { Left = new(Units.Centimeter, 3.18), Top = new(Units.Centimeter, 2.54), Right = new(Units.Centimeter, 3.18), Bottom = new(Units.Centimeter, 2.54) }),
            ("Narrow", new() { Left = new(Units.Centimeter, 1.27), Top = new(Units.Centimeter, 1.27), Right = new(Units.Centimeter, 1.27), Bottom = new(Units.Centimeter, 1.27) }),
            ("Moderate", new() { Left = new(Units.Centimeter, 1.91), Top = new(Units.Centimeter, 2.54), Right = new(Units.Centimeter, 1.91), Bottom = new(Units.Centimeter, 2.54) }),
            ("Wide", new() { Left = new(Units.Centimeter, 5.08), Top = new(Units.Centimeter, 2.54), Right = new(Units.Centimeter, 0), Bottom = new(Units.Centimeter, 5.08) })
        };
    }
}
