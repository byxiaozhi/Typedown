using System.Collections.Generic;

namespace Typedown.Core.Interfaces
{
    public interface IPowerShellService
    {
        IEnumerable<string> Invoke(string script, string command, params string[] parameters);
    }
}
