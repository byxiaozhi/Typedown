namespace Typedown.Automation
{
    /// <summary>
    /// The product's names, in one place, for the app, typedownctl and its MCP server alike. An edition under another
    /// name (Typeleaf, the Microsoft Store one) changes this file, Branding.props, Tools/Installer/brand.iss, the
    /// package manifest and the icons, and nothing else; Tools/Branding/check-brand.py fails when a name is written
    /// anywhere else a person sees it.
    /// </summary>
    public static class Brand
    {
        /// <summary>
        /// Window titles, messages, and the names that keep two editions apart on one machine: the data folders
        /// (Documents\Name, %LOCALAPPDATA%\Name), the single-instance mutex and hand-over pipe, the automation endpoint.
        /// </summary>
        public const string Name = "Typedown";

        /// <summary>The automation command line (and "CliName mcp", the MCP server).</summary>
        public const string CliName = "typedownctl";

        /// <summary>What the MCP server calls itself to an AI tool.</summary>
        public static readonly string McpServerName = Name.ToLowerInvariant() + "-mcp";

        /// <summary>About: the link under the version, and where "Send feedback" goes.</summary>
        public const string AboutLinkText = "GitHub";
        public const string AboutLinkUrl = "https://github.com/flintt/Typedown";
        public const string FeedbackUrl = "https://github.com/flintt/Typedown/issues";
    }
}
