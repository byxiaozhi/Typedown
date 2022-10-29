using System;
using System.Linq;
using System.Reactive;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class Caption : UserControl
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public Caption()
        {
            InitializeComponent();
            if (Config.IsMicaSupported)
                CaptionWrapper.Children.Remove(CaptionWrapper.Children.OfType<CaptionButtons>().First());
        }
    }
}
