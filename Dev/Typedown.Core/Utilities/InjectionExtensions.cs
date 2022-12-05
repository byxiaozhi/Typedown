using Microsoft.Extensions.DependencyInjection;
using System;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Typedown.Core.Utilities
{
    public static class InjectionExtensions
    {
        public static T GetService<T>(this FrameworkElement element)
        {
            try
            {
                if ((element?.XamlRoot?.Content as FrameworkElement)?.DataContext is AppViewModel model)
                    return model.ServiceProvider.GetService<T>();
                return default;
            }
            catch
            {
                return default;
            }
        }

        public static Lazy<T> GetServiceLazy<T>(this FrameworkElement element)
        {
            return new(() => GetService<T>(element));
        }

        public static T GetAncestor<T>(this FrameworkElement element, string name = null) where T : FrameworkElement
        {
            var obj = VisualTreeHelper.GetParent(element);
            while (obj != null)
            {
                if (obj is T res && (string.IsNullOrEmpty(name) || res.Name == name))
                    return res;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return default;
        }
    }
}
