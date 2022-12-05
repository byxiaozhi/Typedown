using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Typedown.Core
{
    public class Resources : ResourceDictionary
    {
        public Resources()
        {
            MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Typedown.Core/Resources/Styles/Button.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Typedown.Core/Resources/Styles/ComboBox.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Typedown.Core/Resources/Styles/ContentDialog.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Typedown.Core/Resources/Styles/ToggleSwitch.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Typedown.Core/Resources/Converters.xaml") });
        }
    }
}
