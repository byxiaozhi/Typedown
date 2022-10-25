using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Typedown.Universal.Interfaces
{
    public interface IWebViewController
    {
        Task InitializeAsync(FrameworkElement container, nint parentHWnd);

        void Navigate(string url);
    }
}
