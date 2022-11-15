using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Typedown.Universal.Models.ExportConfigModels
{
    public class PDFConfigModel : ConfigModel
    {
        public Size PageSize { get; set; }

        public PageMargin Margins { get; set; }

        public string Header { get; set; }

        public string Footer { get; set; }

        public string Author { get; set; }
    }

    public partial class PageSize : INotifyPropertyChanged
    {
        public double Width { get; set; }

        public double Height { get; set; }
    }

    public partial class PageMargin : INotifyPropertyChanged
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }
}
