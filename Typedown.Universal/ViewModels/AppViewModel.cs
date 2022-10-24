using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.ViewModels
{
    public class AppViewModel
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}
