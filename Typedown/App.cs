using System;
using System.Windows;
using Typedown.Windows;

namespace Typedown
{
    public class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new MainWindow().Show();
        }
    }
}
