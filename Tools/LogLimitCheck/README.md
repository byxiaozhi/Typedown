# LogLimitCheck

Proves the local logs stay bounded: `debug.log` rolls over to `debug.old.log` at `Log.DebugLogLimit`, and the
error reports beside it are removed past `Log.ReportAge` or beyond the newest `Log.ReportCount`. Runs on any platform
with the .NET SDK: it compiles `Log.cs` alone, with stand-ins for the two app members it uses.

    cd Tools/LogLimitCheck && dotnet run
