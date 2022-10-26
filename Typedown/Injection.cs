using Microsoft.Extensions.DependencyInjection;
using Typedown.Services;
using Typedown.Utilities;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.ViewModels;

namespace Typedown
{
    public static class Injection
    {
        public static ServiceProvider ServiceProvider { get; }

        static Injection()
        {
            if (ServiceProvider == null)
            {
                var builder = new ServiceCollection();
                RegisterViewModel(builder);
                RegisterService(builder);
                RegisterComponent(builder);
                ServiceProvider = builder.BuildServiceProvider();
            }
        }

        private static void RegisterViewModel(ServiceCollection builder)
        {
            builder.AddScoped<AppViewModel>();
        }

        private static void RegisterService(ServiceCollection builder)
        {
            builder.AddScoped<IWindowService, WindowService>();
            builder.AddScoped<EventCenter>();
        }

        private static void RegisterComponent(ServiceCollection builder)
        {
            builder.AddTransient<IWebViewController, WebViewController>();
        }
    }
}
