using Microsoft.Extensions.DependencyInjection;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Utilities
{
    public static class InjectionExtensions
    {
        public static T GetService<T>(this FrameworkElement element)
        {
            if ((element.DataContext ?? (element.XamlRoot?.Content as FrameworkElement)?.DataContext) is AppViewModel model)
                return model.ServiceProvider.GetService<T>();
            return default;
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
