using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services.Export;
using Typedown.WinUI.Utilities;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // New since the fork: File > Export > Word (docs/word-export-design.md). The text of the document on screen, with every edit the
    // editor has made, is written as a .docx by Services/Export/WordExporter.cs: in this process, with no Word, no network.
    public sealed partial class MainWindow
    {
        private async void ExportWordMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (startPageShown) return;
                var doc = activeDoc;
                // the text the editor holds, not the last saved one; when it does not answer nothing is written (it could be an old text)
                if (!await FlushEditor())
                {
                    await ShowErrorDialog(Locale.GetString("WordExportFailedTitle"), Locale.GetString("WordExportEditorBusy"));
                    return;
                }
                if (!ReferenceEquals(doc, activeDoc)) return;

                var markdown = doc.File.Markdown ?? "";
                string reviewAuthor = null;
                // A tracked document: the changes since the tracking began can go as tracked changes of Word (the text itself is not touched).
                if (doc.Track != null)
                {
                    await RefreshTrack(doc);
                    var tracked = doc.Track?.Last;
                    if (tracked != null && tracked.Changes > 0)
                    {
                        var ask = new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = Locale.GetString("WordExportTrackedTitle"),
                            Content = new TextBlock { Text = Locale.Format("WordExportTrackedMessage", tracked.Changes.ToString()), TextWrapping = TextWrapping.Wrap },
                            PrimaryButtonText = Locale.GetString("WordExportTrackedWith"),
                            SecondaryButtonText = Locale.GetString("WordExportTrackedWithout"),
                            CloseButtonText = Locale.GetString("Cancel"),
                            DefaultButton = ContentDialogButton.Primary,
                        };
                        var answer = await ask.ShowAsync();
                        if (answer == ContentDialogResult.None || !ReferenceEquals(doc, activeDoc)) return;
                        if (answer == ContentDialogResult.Primary)
                        {
                            // the tracking can have moved on while the dialog was open: compare again rather than export an old comparison
                            if (!ReferenceEquals(doc.Track?.Last, tracked))
                            {
                                await RefreshTrack(doc);
                                tracked = doc.Track?.Last;
                            }
                            if (!ReferenceEquals(doc, activeDoc)) return;
                            if (tracked == null || tracked.Changes == 0)
                            {
                                await ShowReviewMessage(Locale.GetString("ReviewNoChanges"));
                                return;
                            }
                            markdown = tracked.Marked;
                            reviewAuthor = doc.Track?.Author;
                        }
                    }
                }

                var choices = await AskWordOptions();
                if (choices == null || !ReferenceEquals(doc, activeDoc)) return;

                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.FileTypeChoices.Add(Locale.GetString("WordDocument"), new List<string> { ".docx" });
                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(doc.DisplayName);
                var picked = await picker.PickSaveFileAsync();
                if (picked == null) return;

                var diagrams = WordExporter.FindDiagrams(markdown);
                if (diagrams.Count > 0) ShowToast(Locale.GetString("WordExportDrawing"), 4000);
                var pictures = diagrams.Count == 0 ? null : await RequestDiagramPictures(diagrams);
                var options = new WordExportOptions
                {
                    BaseFolder = doc.File.ImageBasePath,
                    Language = WordLanguage(),
                    Title = Path.GetFileNameWithoutExtension(doc.DisplayName),
                    Look = (WordLook)choices.Look,
                    PageSize = WordPageSizes[Math.Clamp(choices.PageSize, 0, WordPageSizes.Length - 1)],
                    Landscape = choices.Landscape,
                    Margins = (WordMargins)choices.Margins,
                    HeaderText = choices.HeaderText,
                    FooterText = choices.FooterText,
                    PageNumbers = choices.PageNumbers,
                    TableOfContents = choices.TableOfContents,
                    TemplatePath = string.IsNullOrEmpty(choices.TemplatePath) ? null : choices.TemplatePath,
                    ReviewAuthor = reviewAuthor ?? Environment.UserName,
                    DiagramImages = pictures,
                };
                var result = await Task.Run(() => WordExporter.ExportToFile(markdown, picked.Path, options));
                Log($"ExportWord: {picked.Path}, pictures={result.Pictures}, skipped={result.SkippedPictures.Count}");
                if (result.SkippedPictures.Count == 0)
                {
                    ShowToast(Locale.Format("WordExportDone", picked.Path));
                    return;
                }
                var list = string.Join("\n", result.SkippedPictures);
                await ShowErrorDialog(Locale.GetString("WordExportSkippedTitle"), Locale.Format("WordExportSkippedMessage", picked.Path) + "\n\n" + list);
            }
            catch (WordTemplateException ex)
            {
                Log($"ExportWord: the template could not be used: {ex.Message}");
                await ShowErrorDialog(Locale.GetString("WordExportFailedTitle"), Locale.Format("WordExportTemplateError", ex.Message));
            }
            catch (Exception ex)
            {
                Log($"ExportWord: failed: {ex.Message}");
                await ShowErrorDialog(Locale.GetString("WordExportFailedTitle"), ex.Message);
            }
        }

        private int diagramRequests;

        // The editor draws the diagrams (mermaid, flowchart, sequence, vega-lite) and sends each back as a PNG; null when it does not
        // answer within a minute, and the diagrams stay code in the file. An entry is null when that one could not be drawn.
        private async Task<WordDiagram[]> RequestDiagramPictures(List<(string Type, string Code)> diagrams)
        {
            var id = $"diagrams-{++diagramRequests}";
            var answer = new TaskCompletionSource<JToken>();
            using var subscription = eventCenter.GetObservable<EditorEventArgs>("DiagramsRendered").Subscribe(x =>
            {
                if (x.Args?["id"]?.ToString() == id) answer.TrySetResult(x.Args["images"]);
            });
            PostMessage("RenderDiagrams", new { id, items = diagrams.Select(d => new { type = d.Type, code = d.Code }).ToList() });
            if (await Task.WhenAny(answer.Task, Task.Delay(60000)) != answer.Task)
            {
                Log("ExportWord: the editor did not draw the diagrams");
                return new WordDiagram[diagrams.Count]; // one null each: they stay code and the dialog says so
            }
            if (answer.Task.Result is not JArray images) return new WordDiagram[diagrams.Count];
            return images.Select(image => image?["png"]?.Type == JTokenType.String
                ? new WordDiagram(Convert.FromBase64String(image["png"].ToString()), (double?)image["scale"] ?? 1) : null).ToArray();
        }

        // The language Word proofs the text in: that of the interface, with the region of the PC when it is of that language.
        private static string WordLanguage()
        {
            var lang = Locale.CurrentLang;
            var system = CultureInfo.CurrentUICulture.Name;
            if (system.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase)) return system;
            return lang switch { "fr" => "fr-FR", "es" => "es-ES", "pl" => "pl-PL", "pt" => "pt-BR", _ => "en-US" };
        }
    }
}
