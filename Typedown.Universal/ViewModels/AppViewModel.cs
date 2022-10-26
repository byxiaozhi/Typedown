using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
{
    public class AppViewModel
    {
        public IServiceProvider ServiceProvider { get; }

        public Command<Unit> GoBackCommand = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public AppViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}
