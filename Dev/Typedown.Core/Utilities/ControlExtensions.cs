using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls;

namespace Typedown.Core.Utilities
{
    public static class ControlExtensions
    {
        public static AvaloniaObject GetTemplateChild(this Control control, string childName)
        {
            var method = typeof(Control).GetMethod("GetTemplateChild", BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(control, new object[] { childName }) as AvaloniaObject;
        }
    }
}
