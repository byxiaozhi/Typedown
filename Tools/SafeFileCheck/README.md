# SafeFileCheck

Proves that a failed save never truncates the file: `SafeFile` writes to a temporary file beside the target and
replaces it in one step, and a write that fails leaves the previous version in place. The failing write is
injected with a file name so long that the temporary name exceeds the file system's limit, which works for
root as well. Runs on any platform with the .NET SDK, no app dependencies: it compiles `SafeFile.cs` alone.

    cd Tools/SafeFileCheck && dotnet run
