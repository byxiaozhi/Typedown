using System;
using System.Linq;
using System.Text;

namespace Typedown.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            // typedownctl mcp [--endpoint NAME]: the MCP server (stdio) for AI agents, in the same program.
            if (args.Length > 0 && args[0] == "mcp") return McpHost.Run(args.Skip(1).ToArray());
            return new Cli(Console.In, Console.Out, Console.Error).RunAsync(args).GetAwaiter().GetResult();
        }
    }
}
