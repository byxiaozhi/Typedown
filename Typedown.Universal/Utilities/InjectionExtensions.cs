using Microsoft.Extensions.DependencyInjection;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;

namespace Typedown.Universal.Utilities
{
    public static class InjectionExtensions
    {
        public static T GetService<T>(this FrameworkElement element)
        {
            if (element.DataContext is AppViewModel model)
                return model.ServiceProvider.GetService<T>();
            return default;
        }
    }
}
