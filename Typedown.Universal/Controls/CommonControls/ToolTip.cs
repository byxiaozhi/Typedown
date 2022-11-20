using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public class ToolTip
    {
        public static DependencyProperty TextResourceProperty { get; } = DependencyProperty.Register("TextResource", typeof(string), typeof(ToolTip), new(null, OnResourcePropertyChanged));
        public static string GetTextResource(DependencyObject target) => (string)target.GetValue(TextResourceProperty);
        public static void SetTextResource(DependencyObject target, string value) => target.SetValue(TextResourceProperty, value);

        private static void OnResourcePropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is string resource && !string.IsNullOrEmpty(resource))
                ToolTipService.SetToolTip(target, Localize.GetString(resource));
            else
                ToolTipService.SetToolTip(target, null);
        }
    }
}
