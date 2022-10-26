using Microsoft.Toolkit.Wpf.UI.XamlHost;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Typedown.Utilities;

namespace Typedown.Controls
{
    public class AppXamlHost : WindowsXamlHost
    {
        private static readonly ConditionalWeakTable<global::Windows.UI.Xaml.UIElement, AppXamlHost> instanceTable = new();

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var res = base.BuildWindowCore(hwndParent);
            CoreWindow.DetachCoreWindow();
            return res;
        }

        protected override void OnChildChanged()
        {
            base.OnChildChanged();
            if (GetUwpInternalObject() is global::Windows.UI.Xaml.UIElement element)
                instanceTable.Add(element, this);
        }

        public static AppXamlHost GetAppXamlHost(global::Windows.UI.Xaml.UIElement element)
        {
            return instanceTable.TryGetValue(element.XamlRoot.Content, out var val) ? val : null;
        }
    }
}
