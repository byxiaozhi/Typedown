using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Typedown.Universal.Models
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
