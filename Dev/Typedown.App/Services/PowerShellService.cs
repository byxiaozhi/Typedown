using System.Collections.Generic;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Cross-platform IPowerShellService stub.
/// PowerShell-specific features may be replaced with cross-platform Process calls.
/// </summary>
public class PowerShellService : IPowerShellService
{
    public IEnumerable<string> Invoke(string script, string command, params string[] parameters)
    {
        // TODO: Implement cross-platform process execution
        return new List<string>();
    }
}
