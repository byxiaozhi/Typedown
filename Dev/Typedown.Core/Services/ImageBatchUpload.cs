using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Typedown.Automation;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.Core.Services
{
    /// <summary>
    /// File > Upload local images: every picture of the active document that is a file on this computer goes to the
    /// upload configuration chosen in Settings > Image (each file once, however often the text uses it), and the
    /// document's addresses of the uploaded ones change to their web addresses in one edit, one undo step. What could
    /// not be uploaded keeps its address and is reported. Shows nothing itself; the caller shows progress and result.
    /// </summary>
    public sealed class ImageBatchUpload
    {
        public sealed class Failure
        {
            public string Address { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        public sealed class Result
        {
            /// <summary>Distinct local files the document uses.</summary>
            public int Files { get; set; }
            public int Uploaded { get; set; }
            /// <summary>Of <see cref="Uploaded"/>: uploaded before with this configuration, the earlier address used.</summary>
            public int Reused { get; set; }
            /// <summary>References whose address changed in the document.</summary>
            public int Replaced { get; set; }
            public bool NoConfig { get; set; }
            public bool Cancelled { get; set; }
            public List<Failure> Failures { get; } = new();
            /// <summary>Uploaded, but the document could not take the new addresses (it is listed in Failures too).</summary>
            public string ApplyError { get; set; }
        }

        private const int ApplyAttempts = 3;

        private readonly AppViewModel app;

        public ImageBatchUpload(AppViewModel app)
        {
            this.app = app;
        }

        /// <summary>The local images of a text: address -> the file it names (null when there is none).</summary>
        public Dictionary<string, string> LocalImages(string markdown)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var reference in MarkdownImages.Find(markdown))
                if (MarkdownImages.IsLocal(reference.Address) && !result.ContainsKey(reference.Address))
                    result[reference.Address] = Resolve(reference.Address);
            return result;
        }

        /// <summary>The local images of the active document, from the editor's latest text.</summary>
        public async Task<Dictionary<string, string>> LocalImagesOfActiveDocumentAsync()
        {
            await app.EditorViewModel.FlushContentAsync();
            return LocalImages(app.EditorViewModel.Markdown ?? "");
        }

        public async Task<Result> RunAsync(IProgress<(int done, int total)> progress, CancellationToken cancellationToken)
        {
            var result = new Result();
            var upload = app.ServiceProvider.GetService<ImageUpload>();
            var config = await upload.GetDefaultConfigAsync();
            var tab = app.TabsViewModel.ActiveTab;
            var images = await LocalImagesOfActiveDocumentAsync();
            var files = images.Values.Where(f => f != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result.Files = files.Count + images.Count(i => i.Value == null);
            if (images.Count == 0)
                return result;
            if (config == null)
            {
                result.NoConfig = true;
                return result;
            }

            foreach (var missing in images.Where(i => i.Value == null))
                result.Failures.Add(new Failure { Address = missing.Key, Reason = Locale.GetDialogString("UploadImages.FileNotFound") });

            var uploaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }
                progress?.Report((i, files.Count));
                try
                {
                    var (url, reused) = await upload.UploadRemembered(config, files[i]);
                    if (string.IsNullOrWhiteSpace(url))
                        throw new InvalidOperationException(Locale.GetDialogString("UploadImages.NoAddress"));
                    uploaded[files[i]] = url.Trim();
                    if (reused) result.Reused++;
                }
                catch (Exception ex)
                {
                    foreach (var address in images.Where(x => string.Equals(x.Value, files[i], StringComparison.OrdinalIgnoreCase)).Select(x => x.Key))
                        result.Failures.Add(new Failure { Address = address, Reason = ex.Message });
                }
            }
            if (!result.Cancelled) progress?.Report((files.Count, files.Count));
            result.Uploaded = uploaded.Count;

            var replacements = images.Where(x => x.Value != null && uploaded.ContainsKey(x.Value)).ToDictionary(x => x.Key, x => uploaded[x.Value], StringComparer.Ordinal);
            if (replacements.Count == 0)
                return result;
            try
            {
                if (!app.TabsViewModel.Tabs.Contains(tab))
                    throw new InvalidOperationException(Locale.GetDialogString("UploadImages.DocumentClosed"));
                await ApplyAsync(tab, replacements);
                result.Replaced = replacements.Count;
            }
            catch (Exception ex)
            {
                // The pictures are online but the text still has the old addresses: say where they went.
                Log.Debug($"upload images: the document did not take the new addresses: {ex.Message}");
                result.ApplyError = ex is AutomationException ? Locale.GetDialogString("UploadImages.ApplyFailed") : ex.Message;
                foreach (var r in replacements)
                    result.Failures.Add(new Failure { Address = r.Key, Reason = $"{result.ApplyError} {r.Value}" });
            }
            return result;
        }

        // The addresses are replaced in the latest text, so typing during the upload is kept; the edit is retried
        // when the window wrote to the document between the flush and the edit.
        private async Task ApplyAsync(DocumentTab tab, Dictionary<string, string> replacements)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await DocumentEdits.Coordinator.EditAsync(new AutomationDocument(app, tab), new EditRequest
                    {
                        BaseRevision = tab.Revision,
                        Edit = text => MarkdownImages.Replace(text, replacements),
                        AllowUnknown = true,
                    }, CancellationToken.None);
                    return;
                }
                catch (AutomationException ex) when (ex.Kind == AutomationErrorKind.revision_conflict && attempt < ApplyAttempts)
                {
                    await Task.Delay(200);
                }
            }
        }

        private string Resolve(string address)
        {
            foreach (var candidate in new[] { address, Unescape(address) }.Distinct())
            {
                if (candidate == null || !UriHelper.TryGetLocalPath(candidate, out var path)) continue;
                var full = app.GetImageAbsolutePath(path);
                if (full != null && File.Exists(full)) return full;
            }
            return null;
        }

        private static string Unescape(string address)
        {
            try { return Uri.UnescapeDataString(address); }
            catch { return null; }
        }
    }
}
