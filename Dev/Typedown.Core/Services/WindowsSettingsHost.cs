using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Typedown.Automation;
using Typedown.Core.Enums;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.Core.Services
{
    /// <summary>
    /// The external settings on Windows (Typedown.Automation.ISettingsHost), mapped as docs/automation-fixtures/
    /// settings-map.json says. Values are read from the shared settings store; a change goes through one window's
    /// SettingsViewModel (the same setters the settings page uses), so every window applies it the way it applies a
    /// change made in another window, and then the store is flushed so success means saved.
    /// </summary>
    public sealed class WindowsSettingsHost : ISettingsHost
    {
        private static readonly string[] BuiltIn = { "system", "light", "dark", "black" };

        private JsonSettingsStore Store => JsonSettingsStore.Shared(Path.Combine(Config.GetLocalFolderPath(), "Settings.json"));

        // The stored properties behind the exposed settings: only their changes advance settingsRevision.
        private static readonly ISet<string> Counted = SettingsCatalog.Load().StoredProperties("windows");

        public long Revision => Store.RevisionOf(Counted);

        private static readonly string[] Original = { "appearance.theme", "editor.fontSize", "editor.lineHeight", "editor.textDirection" };

        private static readonly string[] WordCount = { "characters", "words" };

        /// <summary>
        /// The other exposed settings, one line each: how the external value is read from and written to the window's
        /// SettingsViewModel, the same setters the settings page uses. A value this platform cannot take throws
        /// setting_invalid naming the reason.
        /// </summary>
        private static readonly Dictionary<string, (Func<SettingsViewModel, JToken> get, Action<SettingsViewModel, JToken> set)> Simple = new()
        {
            ["ui.language"] = (s => s.Language == "default" ? "system" : s.Language, (s, v) =>
            {
                var code = (string)v;
                if (code == "system") { s.Language = "default"; return; }
                if (!Locale.SupportedLangs.ContainsKey(code)) throw Unsupported("ui.language", "unsupportedLanguage");
                s.Language = code;
            }),
            ["editor.fontFamily"] = (s => s.FontFamily ?? "", (s, v) => s.FontFamily = (string)v),
            ["editor.areaWidth"] = (s => s.EditorAreaWidth, (s, v) => s.EditorAreaWidth = (string)v),
            ["editor.tabSize"] = (s => s.TabSize, (s, v) => s.TabSize = (int)(long)v),
            ["editor.paragraphMarkers"] = (s => s.ShowParagraphMarker, (s, v) => s.ShowParagraphMarker = (bool)v),
            ["editor.autoPairBrackets"] = (s => s.AutoPairBracket, (s, v) => s.AutoPairBracket = (bool)v),
            ["editor.autoPairQuotes"] = (s => s.AutoPairQuote, (s, v) => s.AutoPairQuote = (bool)v),
            ["editor.autoPairMarkdown"] = (s => s.AutoPairMarkdownSyntax, (s, v) => s.AutoPairMarkdownSyntax = (bool)v),
            ["markdown.alignTableColumns"] = (s => s.TableAlignColumns, (s, v) => s.TableAlignColumns = (bool)v),
            ["markdown.listIndentation"] = (s => s.ListIndentation, (s, v) =>
            {
                if ((string)v == "tab") throw Unsupported("markdown.listIndentation", "unsupportedValue");
                s.ListIndentation = (string)v;
            }),
            ["markdown.looseListItems"] = (s => s.PreferLooseListItem, (s, v) => s.PreferLooseListItem = (bool)v),
            ["markdown.trimCodeBlockBlankLines"] = (s => s.TrimUnnecessaryCodeBlockEmptyLines, (s, v) => s.TrimUnnecessaryCodeBlockEmptyLines = (bool)v),
            ["spellcheck.enabled"] = (s => s.SpellcheckEnabled, (s, v) => s.SpellcheckEnabled = (bool)v),
            ["spellcheck.language"] = (s => s.SpellcheckLang ?? "", (s, v) => s.SpellcheckLang = (string)v),
            ["tabs.alwaysShow"] = (s => s.AlwaysShowTabBar, (s, v) => s.AlwaysShowTabBar = (bool)v),
            ["outline.autoExpand"] = (s => s.TocAutoExpand, (s, v) => s.TocAutoExpand = (bool)v),
            ["status.wordCount"] = (s => WordCount[Math.Max(0, Math.Min(WordCount.Length - 1, s.WordCountMethod))], (s, v) =>
            {
                var index = Array.IndexOf(WordCount, (string)v);
                if (index < 0) throw Unsupported("status.wordCount", "unsupportedValue");
                s.WordCountMethod = index;
            }),
            ["images.preferRelativePaths"] = (s => s.PreferRelativeImagePaths, (s, v) => s.PreferRelativeImagePaths = (bool)v),
            ["images.copyRelativePath"] = (s => s.AutoCopyRelativePathImage, (s, v) => s.AutoCopyRelativePathImage = (bool)v),
            ["images.dotSlashPrefix"] = (s => s.AddSymbolBeforeRelativePath, (s, v) => s.AddSymbolBeforeRelativePath = (bool)v),
            ["images.encodeUrls"] = (s => s.AutoEncodeImageURL, (s, v) => s.AutoEncodeImageURL = (bool)v),
            ["appearance.compactMode"] = (s => s.AppCompactMode, (s, v) => s.AppCompactMode = (bool)v),
            ["appearance.mica"] = (s => s.UseMicaEffect, (s, v) =>
            {
                if ((bool)v && !Config.IsMicaSupported) throw Unsupported("appearance.mica", "notSupportedByThisSystem");
                s.UseMicaEffect = (bool)v;
            }),
            ["appearance.editorMica"] = (s => s.UseEditorMicaEffect, (s, v) => s.UseEditorMicaEffect = (bool)v),
            ["appearance.animations"] = (s => s.AnimationEnable, (s, v) => s.AnimationEnable = (bool)v),
            ["app.keepRunning"] = (s => s.KeepRun, (s, v) => s.KeepRun = (bool)v),
        };

        private static AutomationException Unsupported(string key, string reason) =>
            new(AutomationErrorKind.setting_invalid, $"This value of '{key}' is not available here: {reason}.",
                new Dictionary<string, object> { ["key"] = key, ["reason"] = reason });

        public bool Supports(string key) => Original.Contains(key) || Simple.ContainsKey(key);

        private static Task<T> OnFirstWindow<T>(Func<AppViewModel, T> work)
        {
            var window = AutomationWindows.Registry.Snapshot().FirstOrDefault()
                ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, Typedown.Automation.Brand.Name + " has no open window to apply settings in.");
            return AutomationWindows.Registry.OnWindowAsync(window.WindowId, work);
        }

        public Task<JToken> GetAsync(string key, CancellationToken cancellationToken)
        {
            if (Simple.TryGetValue(key, out var simple)) return OnFirstWindow(app => simple.get(app.SettingsViewModel));
            var store = Store;
            JToken value = key switch
            {
                "appearance.theme" => ThemeValue(store.Get("AppTheme", AppTheme.Default), store.Get("CustomTheme", "")),
                "editor.fontSize" => (int)Math.Round(store.Get("FontSize", 16d)),
                "editor.lineHeight" => Math.Round(store.Get("LineHeight", 1.6d), 6),
                "editor.textDirection" => store.Get("TextDirection", "auto"),
                _ => throw new AutomationException(AutomationErrorKind.setting_not_exposed, $"'{key}' is not an external setting."),
            };
            return Task.FromResult(value);
        }

        private static JObject ThemeValue(AppTheme theme, string custom) => string.IsNullOrEmpty(custom)
            ? new JObject { ["kind"] = "builtIn", ["id"] = BuiltIn[Math.Max(0, Math.Min(BuiltIn.Length - 1, (int)theme))] }
            : new JObject { ["kind"] = "custom", ["id"] = custom };

        public async Task<long> SetAsync(string key, JToken value, CancellationToken cancellationToken)
        {
            // Resolve a custom theme before anything changes: an unknown one changes nothing.
            CustomTheme customTheme = null;
            if (key == "appearance.theme" && (string)value["kind"] == "custom")
                customTheme = ThemeFiles.Find((string)value["id"])
                    ?? throw new AutomationException(AutomationErrorKind.setting_invalid, "No custom theme with that id.",
                        new Dictionary<string, object> { ["key"] = key, ["reason"] = "unknownTheme" });

            void Apply(SettingsViewModel settings)
            {
                switch (key)
                {
                    case "appearance.theme":
                        if (customTheme != null) settings.ApplyCustomTheme(customTheme);
                        else settings.ApplyBuiltInTheme((AppTheme)Array.IndexOf(BuiltIn, (string)value["id"]));
                        break;
                    case "editor.fontSize": settings.FontSize = (long)value; break;
                    case "editor.lineHeight": settings.LineHeight = (double)value; break;
                    case "editor.textDirection": settings.TextDirection = (string)value; break;
                    case var other when Simple.TryGetValue(other, out var simple): simple.set(settings, value); break;
                    default: throw new AutomationException(AutomationErrorKind.setting_not_exposed, $"'{key}' is not an external setting.");
                }
            }

            var window = AutomationWindows.Registry.Snapshot().FirstOrDefault()
                ?? throw new AutomationException(AutomationErrorKind.editor_not_ready, Typedown.Automation.Brand.Name + " has no open window to apply settings in.");
            await AutomationWindows.Registry.OnWindowAsync(window.WindowId, app => { Apply(app.SettingsViewModel); return true; });

            var store = Store;
            await store.FlushAsync();
            if (store.LastWriteError != null)
                throw new AutomationException(AutomationErrorKind.persistence_failed, "The setting is applied but could not be saved.",
                    new Dictionary<string, object> { ["settingsRevision"] = Revision, ["applied"] = true });
            return Revision;
        }
    }
}
