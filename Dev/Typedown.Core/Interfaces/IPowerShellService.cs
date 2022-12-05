using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Core.Interfaces
{
    public interface IPowerShellService
    {
        IEnumerable<string> Invoke(string script, string command, params string[] parameters);
    }
}
