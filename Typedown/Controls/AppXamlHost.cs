using Microsoft.Toolkit.Wpf.UI.XamlHost;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Typedown.Controls
{
    public class AppXamlHost : WindowsXamlHost
    {
        private static readonly ConditionalWeakTable<global::Windows.UI.Xaml.UIElement, AppXamlHost> appXamlHostTable = new();

        protected override void OnChildChanged()
        {
            base.OnChildChanged();
            if (GetUwpInternalObject() is global::Windows.UI.Xaml.UIElement element)
                appXamlHostTable.Add(element, this);
        }

        public static AppXamlHost GetAppXamlHost(global::Windows.UI.Xaml.UIElement element)
        {
            return appXamlHostTable.TryGetValue(element.XamlRoot.Content, out var val) ? val : null;
        }
    }
}
