using System.Linq;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>typedownctl against a real pipe server and the fake host.</summary>
    public class CliTests : IAsyncLifetime
    {
        private readonly string endpoint = "td-cli-" + Guid.NewGuid().ToString("N");
        private readonly FakeHost host = new();
        private readonly FakeViewHost view = new();
        private AutomationServer server = null!;
        private FakeDocument doc = null!;

        public Task InitializeAsync()
        {
            var info = new ServerInfo { Version = "1.2.30", Platform = "test" };
            server = new AutomationServer(() => new PlainPipeListener(endpoint),
                () => new AutomationSession(info, ViewMethods.AddTo(DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, info.InstanceId), view)), info.MaxMessageBytes);
            doc = host.Add("# Title\n\nold text\n", "/tmp/a.md");
            server.Start();
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => server.StopAsync();

        private async Task<(int exit, string stdout, string stderr)> Run(string stdin, params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var all = new System.Collections.Generic.List<string>(args) { "--endpoint", endpoint, "--client-id", "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c" };
            var exit = await new Cli.Cli(new StringReader(stdin), output, error).RunAsync(all.ToArray()).WaitAsync(TimeSpan.FromSeconds(20));
            return (exit, output.ToString(), error.ToString());
        }

        [Fact]
        public async Task Reads_print_one_json_value()
        {
            var (exit, stdout, _) = await Run("", "--json", "documents");
            Assert.Equal(0, exit);
            var documents = JObject.Parse(stdout)["documents"]!;
            Assert.Equal(doc.DocumentId, (string)documents[0]!["documentId"]!);
            (exit, stdout, _) = await Run("", "get", doc.DocumentId, "--latest", "--text");
            Assert.Equal(0, exit);
            Assert.Equal("# Title\n\nold text\n", stdout);
            (exit, stdout, _) = await Run("", "--json", "status");
            Assert.Equal("1.2.30", (string)JObject.Parse(stdout)["version"]!);
        }

        [Fact]
        public async Task Replace_from_stdin_then_a_stale_revision_is_a_conflict()
        {
            var (exit, stdout, _) = await Run("# Title\n\nnew text\n", "--json", "replace", doc.DocumentId, "--base-revision", "0", "--stdin");
            Assert.Equal(0, exit);
            Assert.Equal(1, (int)JObject.Parse(stdout)["revision"]!);
            Assert.Equal("# Title\n\nnew text\n", doc.Text);
            (exit, stdout, var stderr) = await Run("x\n", "--json", "replace", doc.DocumentId, "--base-revision", "0", "--stdin");
            Assert.Equal(Cli.Cli.Conflict, exit);
            Assert.Equal("revision_conflict", (string)JObject.Parse(stdout)["error"]!["data"]!["kind"]!);
            Assert.Contains("revision_conflict", stderr);
        }

        [Fact]
        public async Task Reveal_change_asks_for_the_change_to_be_scrolled_into_view()
        {
            var (exit, _, _) = await Run("", "replace-text", doc.DocumentId, "--base-revision", "0", "--find", "old", "--replacement", "new", "--expected-count", "1", "--reveal-change");
            Assert.Equal(0, exit);
            Assert.True(doc.LastApply!.ScrollToChange);
            Assert.Equal(Reveal.Document, host.Reveals[^1]);
            (exit, _, _) = await Run("", "replace-text", doc.DocumentId, "--base-revision", "1", "--find", "new", "--replacement", "old", "--expected-count", "1", "--reveal");
            Assert.Equal(0, exit);
            Assert.False(doc.LastApply!.ScrollToChange);
            (exit, _, _) = await Run("", "replace-text", doc.DocumentId, "--base-revision", "2", "--find", "old", "--replacement", "new", "--expected-count", "1", "--reveal", "--reveal-change");
            Assert.Equal(Cli.Cli.Usage, exit);
            Assert.Equal(2, doc.Revision);
        }

        [Fact]
        public async Task Replace_text_counts_and_crlf_needs_lf()
        {
            var (exit, _, _) = await Run("", "replace-text", doc.DocumentId, "--base-revision", "0", "--find", "text", "--replacement", "words", "--expected-count", "2");
            Assert.Equal(Cli.Cli.Conflict, exit);
            (exit, _, _) = await Run("", "replace-text", doc.DocumentId, "--base-revision", "0", "--find", "text", "--replacement", "words", "--expected-count", "1");
            Assert.Equal(0, exit);
            Assert.Equal("# Title\n\nold words\n", doc.Text);
            (exit, _, _) = await Run("a\r\nb\r\n", "replace", doc.DocumentId, "--base-revision", "1", "--stdin");
            Assert.Equal(Cli.Cli.Usage, exit);
            (exit, _, _) = await Run("a\r\nb\r\n", "replace", doc.DocumentId, "--base-revision", "1", "--stdin", "--lf");
            Assert.Equal(0, exit);
            Assert.Equal("a\nb\n", doc.Text);
        }

        [Fact]
        public async Task Unknown_normalization_needs_the_explicit_flag()
        {
            doc.Page = c => FakeDocument.Classified(c, PendingNormalization.Unknown, "formatting-changed");
            var (exit, stdout, _) = await Run("* b\n", "--json", "replace", doc.DocumentId, "--base-revision", "0", "--stdin");
            Assert.Equal(Cli.Cli.Other, exit);
            Assert.Equal("normalization_unclassified", (string)JObject.Parse(stdout)["error"]!["data"]!["kind"]!);
            (exit, _, _) = await Run("* b\n", "replace", doc.DocumentId, "--base-revision", "0", "--stdin", "--allow-unknown-normalization");
            Assert.Equal(0, exit);
        }

        [Fact]
        public async Task Usage_errors_and_a_missing_app_have_their_own_exit_codes()
        {
            Assert.Equal(Cli.Cli.Usage, (await Run("", "replace", doc.DocumentId, "--stdin")).exit); // no --base-revision
            Assert.Equal(Cli.Cli.Usage, (await Run("", "documents", "--bogus", "1")).exit);
            Assert.Equal(Cli.Cli.Usage, (await Run("", "frobnicate")).exit);
            Assert.Equal(Cli.Cli.NotFound, (await Run("", "get", "00000000000000000000000000000000")).exit);
            var output = new StringWriter();
            var exit = await new Cli.Cli(new StringReader(""), output, new StringWriter()).RunAsync(new[] { "--json", "--endpoint", "td-nothing-" + Guid.NewGuid().ToString("N"), "status" });
            Assert.Equal(Cli.Cli.NotRunning, exit);
            Assert.Equal("cli_not_connected", (string)JObject.Parse(output.ToString())["error"]!["data"]!["kind"]!);
        }

        [Fact]
        public async Task Save_asks_only_for_the_save_scope_and_untitled_documents_need_a_path()
        {
            var (exit, stdout, _) = await Run("", "--json", "save", doc.DocumentId);
            Assert.Equal(0, exit);
            Assert.True((bool)JObject.Parse(stdout)["saved"]!);
            var untitled = host.Add("u\n", null, active: false);
            (exit, stdout, _) = await Run("", "--json", "save", untitled.DocumentId);
            Assert.Equal("path_required", (string)JObject.Parse(stdout)["error"]!["data"]!["kind"]!);
        }

        [Theory]
        [InlineData("appearance.theme", "{\"kind\":\"builtIn\",\"id\":\"dark\"}", "{\"kind\":\"builtIn\",\"id\":\"dark\"}")]
        [InlineData("appearance.theme", "{kind:builtIn,id:dark}", "{\"kind\":\"builtIn\",\"id\":\"dark\"}")]   // Windows PowerShell 5 dropped the quotes
        [InlineData("appearance.theme", "{ kind: custom, id: solarized-light }", "{\"kind\":\"custom\",\"id\":\"solarized-light\"}")]
        [InlineData("appearance.theme", "{kind:custom,id:My Theme}", "{\"kind\":\"custom\",\"id\":\"My Theme\"}")]   // a space in the id
        [InlineData("appearance.theme", "{kind:custom,id:\"My Theme\"}", "{\"kind\":\"custom\",\"id\":\"My Theme\"}")]   // quotes kept by another shell
        [InlineData("appearance.theme", "{kind:custom,id:2024}", "{\"kind\":\"custom\",\"id\":2024}")]
        [InlineData("appearance.theme", "dark", "{\"kind\":\"builtIn\",\"id\":\"dark\"}")]
        [InlineData("appearance.theme", "custom:sepia", "{\"kind\":\"custom\",\"id\":\"sepia\"}")]
        [InlineData("editor.textDirection", "rtl", "\"rtl\"")]
        [InlineData("editor.fontSize", "18", "18")]
        [InlineData("editor.autoPairQuotes", "false", "false")]
        [InlineData("status.wordCount", "dark", "\"dark\"")]   // the theme's short form is the theme's only
        public void Setting_values_take_json_bare_words_the_theme_short_form_and_json_without_its_quotes(string key, string typed, string sent) =>
            Assert.Equal(sent, Cli.Cli.SettingValue(key, typed).ToString(Newtonsoft.Json.Formatting.None));

        [Fact]
        public void Exit_codes_follow_the_spec_table()
        {
            Assert.Equal(4, Cli.Cli.ExitCodeFor("scope_required"));
            Assert.Equal(5, Cli.Cli.ExitCodeFor("document_not_found"));
            Assert.Equal(6, Cli.Cli.ExitCodeFor("match_count_mismatch"));
            Assert.Equal(7, Cli.Cli.ExitCodeFor("content_sync_timeout"));
            Assert.Equal(8, Cli.Cli.ExitCodeFor("save_failed"));
            Assert.Equal(9, Cli.Cli.ExitCodeFor("content_not_roundtrippable"));
        }

        [Fact]
        public async Task Close_closes_a_saved_document_and_refuses_an_unsaved_one_with_exit_6()
        {
            var other = host.Add("# Other\n", "/tmp/b.md");
            other.Saved = false;
            var (exit, _, stderr) = await Run("", "close", other.DocumentId);
            Assert.Equal(6, exit);
            Assert.Contains("unsaved_changes", stderr);
            (exit, _, _) = await Run("", "close", doc.DocumentId);
            Assert.Equal(0, exit);
            Assert.DoesNotContain(host.Docs, d => d.doc == doc);
        }

        [Fact]
        public async Task View_reads_without_options_and_sets_with_them()
        {
            var (exit, stdout, _) = await Run("", "view", FakeHost.WindowId);
            Assert.Equal(0, exit);
            Assert.Contains("mode visual  side pane closed  status bar on", stdout);
            Assert.Contains("bounds 10,20,1200,800", stdout);
            (exit, stdout, _) = await Run("", "--json", "view", FakeHost.WindowId, "--mode", "reading", "--side-pane", "outline", "--size", "1280x860");
            Assert.Equal(0, exit);
            var result = JObject.Parse(stdout);
            Assert.Equal("reading", (string)result["mode"]!);
            Assert.Equal(1280, (int)result["bounds"]!["width"]!);
            Assert.Equal(860, (int)view.State.Height);
            (exit, _, _) = await Run("", "view", FakeHost.WindowId, "--side-pane", "left");
            Assert.Equal(2, exit);
            (exit, _, _) = await Run("", "view", FakeHost.WindowId, "--bounds", "1,2,3");
            Assert.Equal(2, exit);
            (exit, _, _) = await Run("", "view", FakeHost.WindowId, "--size", "300x300");
            Assert.Equal(2, exit);
        }
    }
}

namespace Typedown.Automation.Tests
{
    public class CliSettingsTests : IAsyncLifetime
    {
        private readonly string endpoint = "td-cli-s-" + Guid.NewGuid().ToString("N");
        private readonly FakeSettingsHost host = new();
        private AutomationServer server = null!;

        public Task InitializeAsync()
        {
            var info = new ServerInfo();
            server = new AutomationServer(() => new PlainPipeListener(endpoint),
                () => new AutomationSession(info, SettingsMethods.AddTo(new MethodTable(BuildTypes.Application), host, SettingsCatalog.Load())), info.MaxMessageBytes);
            server.Start();
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => server.StopAsync();

        private async Task<(int exit, string stdout)> Run(params string[] args)
        {
            var output = new System.IO.StringWriter();
            var all = new System.Collections.Generic.List<string>(args) { "--endpoint", endpoint, "--client-id", "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c" };
            var exit = await new Cli.Cli(new System.IO.StringReader(""), output, new System.IO.StringWriter()).RunAsync(all.ToArray()).WaitAsync(TimeSpan.FromSeconds(20));
            return (exit, output.ToString());
        }

        [Fact]
        public async Task Settings_commands_read_and_write()
        {
            var (exit, stdout) = await Run("--json", "settings", "get", "editor.fontSize");
            Assert.Equal(0, exit);
            Assert.Equal(16, (int)Newtonsoft.Json.Linq.JObject.Parse(stdout)["values"]!["editor.fontSize"]!);
            (exit, stdout) = await Run("--json", "settings", "set", "editor.textDirection", "rtl", "--base-revision", "7");
            Assert.Equal(0, exit);
            Assert.Equal("rtl", (string)host.Values["editor.textDirection"]);
            (exit, _) = await Run("settings", "set", "editor.fontSize", "99", "--base-revision", "8");
            Assert.Equal(Cli.Cli.Other, exit); // setting_invalid
            (exit, _) = await Run("settings", "set", "editor.fontSize", "20", "--base-revision", "1");
            Assert.Equal(Cli.Cli.Conflict, exit);
            (exit, stdout) = await Run("--json", "settings", "describe");
            Assert.Equal(SettingsCatalog.Load().Settings.Count(x => new FakeSettingsHost().Supports(x.Key)), ((Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(stdout)["settings"]!).Count);
        }

    }
}
