using Typedown.Core.Enums;
using System.Collections.Generic;
using Avalonia.Controls;

namespace Typedown.Core.Models
{
    public class PdfPrintSettings
    {
        public PrintOrientation Orientation { get; set; }

        public double? ScaleFactor { get; set; }

        public Size? PageSize { get; set; }

        public Thickness? Margin { get; set; }

        public bool ShouldPrintHeaderAndFooter { get; set; }

        public string Header { get; set; } = string.Empty;

        public string Footer { get; set; } = string.Empty;
    }
}
