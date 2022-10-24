using Microsoft.Extensions.DependencyInjection;
using System;
using Typedown.Services;
using Typedown.Universal.Interfaces;
using Typedown.Universal.ViewModels;

namespace Typedown.Windows
{
    public partial class MainWindow : AppWindow
    {
        public IServiceScope ServiceScope { get; } = Injection.ServiceProvider.CreateScope();

        public IServiceProvider ServiceProvider => ServiceScope.ServiceProvider;

        public AppViewModel AppViewModel { get; }

        public MainWindow()
        {
            AppViewModel = ServiceProvider.GetService<AppViewModel>();
            DataContext = AppViewModel;
            InitializeComponent();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            (ServiceProvider.GetService<IWindowService>() as WindowService).RaiseWindowStateChanged(Handle);
        }
    }
}
