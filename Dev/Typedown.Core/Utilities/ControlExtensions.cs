using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Utilities
{
    public static class ControlExtensions
    {
        public static DependencyObject GetTemplateChild(this Control control, string childName)
        {
            var method = typeof(Control).GetMethod("GetTemplateChild", BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(control, new object[] { childName }) as DependencyObject;
        }
    }
}
