using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.Core.ViewModels
{
    public sealed partial class FileViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetService<SettingsViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public AccessHistory AccessHistory => ServiceProvider.GetService<AccessHistory>();

        public string WorkFolder { get; private set; } = null;

        public string FilePath { get; private set; } = null;

        public string ImageBasePath => string.IsNullOrEmpty(FilePath) ? SettingsViewModel.DefaultImageBasePath : Path.GetDirectoryName(FilePath);

        public string FileName => Path.GetFileName(FilePath);

        public Command<Unit> NewFileCommand { get; } = new();
        public Command<string> NewWindowCommand { get; } = new();
        public Command<string> OpenFileCommand { get; } = new();
        public Command<string> OpenFolderCommand { get; } = new();
        public Command<Unit> NewFolderCommand { get; } = new();
        public Command<Unit> ClearHistoryCommand { get; } = new();
        public Command<Unit> SaveCommand { get; } = new();
        public Command<Unit> SaveAsCommand { get; } = new();
        public Command<Unit> ImportCommand { get; } = new();
        public Command<ExportConfig> ExportCommand { get; } = new();
        public Command<Unit> PrintCommand { get; } = new();
        public Command<Unit> ExitCommand { get; } = new();

        private System.Threading.Timer saveFileTimer;

        public AutoBackup AutoBackup => ServiceProvider.GetService<AutoBackup>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        private IDialogService DialogService => ServiceProvider.GetService<IDialogService>();

        private IFilePickerService FilePickerService => ServiceProvider.GetService<IFilePickerService>();

        private readonly CompositeDisposable disposables = new();

        public FileViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            NewFileCommand.OnExecute.Subscribe(async _ => await NewFileFun());
            OpenFileCommand.OnExecute.Subscribe(async x => await OpenFile(x));
            OpenFolderCommand.OnExecute.Subscribe(async x => await OpenFolder(x));
            SaveAsCommand.OnExecute.Subscribe(async _ => await SaveAs());
            SaveCommand.OnExecute.Subscribe(async _ => await Save());
            ExitCommand.OnExecute.Subscribe(_ => { /* Platform handles close */ });
            ClearHistoryCommand.OnExecute.Subscribe(x => { _ = AccessHistory.ClearHistory(); });
            ExportCommand.OnExecute.Subscribe(Export);
            PrintCommand.OnExecute.Subscribe(_ => Print());
            ImportCommand.OnExecute.Subscribe(_ => Import());
            RemoteInvoke.Handle<JToken, bool>("ExportCallback", ExportCallback);
            RemoteInvoke.Handle<JToken, bool>("PrintHTML", PrintHTML);
            saveFileTimer = new System.Threading.Timer(SaveFileTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private async void SaveFileTimerTick(object state)
        {
            try
            {
                if (disposables.IsDisposed) return;
                if (SettingsViewModel.AutoSave)
                {
                    EditorViewModel.AutoSavedSucc = await AutoSaveFile();
                    if (!EditorViewModel.AutoSavedSucc)
                        await AutoBackupFile();
                }
                else
                {
                    await AutoBackupFile();
                }
            }
            catch { /* Ignore timer errors */ }
        }

        public async Task<bool> AutoSaveFile()
        {
            try
            {
                if (SettingsViewModel.AutoSave && EditorViewModel.FileLoaded && (EditorViewModel.FileHash != EditorViewModel.CurrentHash) && FilePath != null)
                    return await Save(false);
                return FilePath != null;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> AutoBackupFile()
        {
            if (EditorViewModel.FileHash != EditorViewModel.CurrentHash && !string.IsNullOrWhiteSpace(EditorViewModel.Markdown))
                return await AutoBackup.Backup(FilePath, EditorViewModel.Markdown);
            else
                AutoBackup.DeleteBackup(FilePath);
            return true;
        }

        private async Task NewFileFun(bool postMessage = true)
        {
            if (!await AskToSave()) return;
            FilePath = null;
            EditorViewModel.FileHash = Common.SimpleHash(Common.DefaultMarkdwn);
            string backup = null;
            if (AppViewModel.GetInstances().Where(x => x != AppViewModel).All(x => !string.IsNullOrEmpty(x.FileViewModel.FilePath)))
            {
                backup = await CheckBackup(FilePath, EditorViewModel.FileHash);
            }
            if (backup == null)
            {
                EditorViewModel.Markdown = Common.DefaultMarkdwn;
                EditorViewModel.CurrentHash = EditorViewModel.FileHash;
                EditorViewModel.Saved = true;
                EditorViewModel.AutoSavedSucc = true;
                EditorViewModel.FileLoaded = false;
            }
            else
            {
                EditorViewModel.Markdown = backup;
                EditorViewModel.CurrentHash = Common.SimpleHash(backup);
                EditorViewModel.Saved = false;
                EditorViewModel.AutoSavedSucc = false;
                EditorViewModel.FileLoaded = true;
            }
            EditorViewModel.History.InitHistory(Common.DefaultMarkdwn);
            if (postMessage)
            {
                MarkdownEditor?.PostMessage("LoadFile", EditorViewModel.Markdown);
            }
        }

        public async Task<bool> OpenFile(string filePath = null)
        {
            if (!await AskToSave())
                return false;
            filePath ??= await FilePickerService?.PickOpenFileAsync(FileTypeHelper.Markdown.ToArray(), "Open Markdown File");
            if (filePath == null)
                return false;
            return await LoadFile(filePath, true);
        }

        public async Task<bool> OpenFolder(string folderPath = null)
        {
            folderPath ??= await FilePickerService?.PickFolderAsync("Open Folder");
            if (folderPath == null)
                return false;
            if (!await LoadFolder(folderPath))
                return false;
            SettingsViewModel.SidePaneOpen = true;
            SettingsViewModel.SidePaneIndex = 0;
            return true;
        }

        private async Task<bool> LoadFile(string path, bool skipSavedCheck = false, bool postMessage = true)
        {
            try
            {
                if (TryGetOpenedWindow(path, out _))
                {
                    return false;
                }
                if (!File.Exists(path))
                {
                    _ = AccessHistory.RemoveFileHistory(path);
                    throw new FileNotFoundException("File does not exist.");
                }
                else if (!skipSavedCheck && !await AskToSave())
                {
                    return false;
                }
                var text = await File.ReadAllTextAsync(path);
                EditorViewModel.FirstStart = false;
                EditorViewModel.FileHash = Common.SimpleHash(text);
                FilePath = path;
                _ = AccessHistory.RecordFileHistory(FilePath);
                var backup = await CheckBackup(path, EditorViewModel.CurrentHash);
                if (backup == null)
                {
                    EditorViewModel.Markdown = text;
                    EditorViewModel.CurrentHash = EditorViewModel.FileHash;
                    EditorViewModel.Saved = true;
                    EditorViewModel.FileLoaded = false;
                }
                else
                {
                    EditorViewModel.Markdown = backup;
                    EditorViewModel.CurrentHash = Common.SimpleHash(backup);
                    EditorViewModel.Saved = false;
                    EditorViewModel.FileLoaded = true;
                }
                EditorViewModel.AutoSavedSucc = true;
                EditorViewModel.History.InitHistory(EditorViewModel.Markdown);
                if (postMessage)
                {
                    MarkdownEditor?.PostMessage("LoadFile", new { text = EditorViewModel.Markdown, basePath = ImageBasePath });
                }
                return true;
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetDialogString("ReadErrorTitle"), ex.Message, Locale.GetDialogString("Ok"));
                return false;
            }
        }

        private async Task<bool> LoadFolder(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    _ = AccessHistory.RemoveFolderHistory(folderPath);
                    throw new FileNotFoundException("Folder does not exist.");
                }
                WorkFolder = folderPath;
                _ = AccessHistory.RecordFolderHistory(folderPath);
                return true;
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok"));
                return false;
            }
        }

        private async Task<string> CheckBackup(string path, ulong fileHash)
        {
            string text = await AutoBackup.GetBackup(path);
            if (text == null || Common.SimpleHash(text) == fileHash) return null;
            if (DialogService == null) return null;
            var result = await DialogService.ShowAsync(
                Locale.GetDialogString("RecoverTitle"),
                Locale.GetDialogString("RecoverContent"),
                Locale.GetDialogString("Cancel"),
                Locale.GetDialogString("Recover"),
                Locale.GetDialogString("Delete"));
            if (result == DialogResult.Primary)
            {
                return text;
            }
            else
            {
                AutoBackup.DeleteBackup(path);
                return null;
            }
        }

        private async Task<bool> WriteAllText(string path, string text, bool alert = true)
        {
            try
            {
                await File.WriteAllTextAsync(path, text);
                return true;
            }
            catch (Exception ex)
            {
                if (alert && DialogService != null)
                {
                    await DialogService.ShowAsync(Locale.GetDialogString("SaveErrorTitle"), ex.Message, Locale.GetDialogString("Ok"));
                }
                return false;
            }
        }

        private async Task<bool> Save(bool alert = true)
        {
            if (FilePath == null)
            {
                var result = await SaveAs();
                return result != null;
            }
            else
            {
                var result = await WriteAllText(FilePath, EditorViewModel.Markdown, alert);
                if (result)
                {
                    EditorViewModel.FileHash = EditorViewModel.CurrentHash;
                    EditorViewModel.Saved = true;
                    AutoBackup.DeleteBackup(FilePath);
                    _ = AccessHistory.RecordFileHistory(FilePath);
                }
                return result;
            }
        }

        private async Task<string> SaveAs()
        {
            try
            {
                var filePath = await FilePickerService?.PickSaveFileAsync(
                    FileName ?? "untitled",
                    FileTypeHelper.Markdown.ToArray(),
                    "Save Markdown File");
                if (filePath != null)
                {
                    var result = await WriteAllText(filePath, EditorViewModel.Markdown);
                    if (result)
                    {
                        AutoBackup.DeleteBackup(FilePath);
                        FilePath = filePath;
                        EditorViewModel.FileHash = EditorViewModel.CurrentHash;
                        EditorViewModel.Saved = true;
                        _ = AccessHistory.RecordFileHistory(FilePath);
                        return filePath;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok"));
                return null;
            }
        }

        private async Task<bool> PrintHTML(JToken args)
        {
            try
            {
                var html = args["html"].ToString();
                var fileExport = ServiceProvider.GetService<IFileExport>();
                await fileExport.Print(Path.GetDirectoryName(FilePath), html, FileName);
                return true;
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok"));
                return false;
            }
        }

        private async Task<bool> ExportCallback(JToken args)
        {
            try
            {
                var html = args["html"].ToString();
                var filePath = args["context"]["filePath"].ToString();
                var configId = args["context"]["configId"].ToObject<int>();
                var config = await ServiceProvider.GetService<IFileExport>().GetExportConfig(configId);
                await config.LoadExportConfig().Export(ServiceProvider, html, filePath);
                if (SettingsViewModel.OpenFolderAfterExport)
                    Common.OpenFileLocation(filePath);
                return true;
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok"));
                return false;
            }
        }

        private bool askToSaveOpened;

        public async Task<bool> AskToSave()
        {
            if (EditorViewModel.Saved || (SettingsViewModel.AutoSave && await AutoSaveFile()))
            {
                return true;
            }
            if (askToSaveOpened)
            {
                return false;
            }
            if (DialogService == null) return true;
            askToSaveOpened = true;
            var result = await DialogService.ShowAsync(
                Locale.GetDialogString("AsKToSaveTitle"),
                Locale.GetDialogString("AsKToSaveContent"),
                Locale.GetDialogString("Cancel"),
                Locale.GetDialogString("Save"),
                Locale.GetDialogString("Don'tSave"));
            askToSaveOpened = false;
            switch (result)
            {
                case DialogResult.Primary:
                    var saveResult = await Save();
                    return saveResult;
                case DialogResult.Secondary:
                    AutoBackup.DeleteBackup(FilePath);
                    return true;
                case DialogResult.None:
                    return false;
            }
            return false;
        }

        public async Task LoadStartUpMarkdown()
        {
            var path = CommandLine.GetOpenFilePath(AppViewModel.CommandLineArgs);
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    await LoadFile(path, true, false);
                }
                catch (Exception)
                {
                    await NewFileFun(false);
                }
            }
            else
            {
                switch (SettingsViewModel.FileStartupAction)
                {
                    case Enums.FileStartupAction.OpenLast:
                        await AccessHistory.EnsureInitialized();
                        if (AccessHistory.FileRecentlyOpened.FirstOrDefault() is string lastFile && !TryGetOpenedWindow(lastFile, out _) && File.Exists(lastFile))
                            await LoadFile(lastFile, true);
                        break;
                    default:
                        await NewFileFun(false);
                        break;
                }
            }
        }

        private async void Export(ExportConfig config)
        {
            var filePath = await FilePickerService?.PickSaveFileAsync(
                "export",
                config.FileExtensions.Select(x => x.extension).ToArray(),
                "Export File");
            if (filePath == null) return;
            string basePath = null;
            if (config.Type == Enums.ExportType.PDF || config.Type == Enums.ExportType.Image)
                basePath = ImageBasePath;
            MarkdownEditor?.PostMessage("Export", new
            {
                type = "export",
                title = Path.GetFileNameWithoutExtension(filePath),
                context = new { configId = config.Id, filePath },
                basePath,
                options = config.LoadExportConfig()
            });
        }

        private void Print()
        {
            MarkdownEditor?.PostMessage("Export", new
            {
                type = "print",
                basePath = ImageBasePath,
                title = FileName ?? "untitled"
            });
        }

        private async void Import()
        {
            try
            {
                var filePath = await FilePickerService?.PickOpenFileAsync(new[] { ".html" }, "Import File");
                if (filePath != null)
                {
                    var text = await File.ReadAllTextAsync(filePath);
                    MarkdownEditor?.PostMessage("ImportFile", new { type = Path.GetExtension(filePath).Substring(1), text });
                }
            }
            catch (Exception ex)
            {
                if (DialogService != null)
                    await DialogService.ShowAsync(Locale.GetDialogString("ImportErrorTitle"), ex.Message, Locale.GetDialogString("Ok"));
            }
        }

        private async void OnStartup()
        {
            try
            {
                if (string.IsNullOrEmpty(WorkFolder))
                {
                    switch (SettingsViewModel.FolderStartupAction)
                    {
                        case Enums.FolderStartupAction.OpenLast:
                            await AccessHistory.EnsureInitialized();
                            if (AccessHistory.FolderRecentlyOpened.FirstOrDefault() is string lastFolder && Directory.Exists(lastFolder))
                                await LoadFolder(lastFolder);
                            break;
                        case Enums.FolderStartupAction.OpenFolder:
                            if (Directory.Exists(SettingsViewModel.StartupOpenFolder))
                                await LoadFolder(SettingsViewModel.StartupOpenFolder);
                            break;
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        public static bool TryGetOpenedWindow(string filePath, out IntPtr window)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                window = default;
                return false;
            }
            window = AppViewModel.GetInstances().Where(x => x.FileViewModel.FilePath?.ToLower() == filePath.ToLower()).FirstOrDefault()?.MainWindow ?? default;
            return window != default;
        }

        public bool RenameFile(string to)
        {
            if (!File.Exists(FilePath))
                return false;
            var fileOperation = ServiceProvider.GetService<IFileOperation>();
            if (fileOperation.Rename(FilePath, to))
            {
                FilePath = to;
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            saveFileTimer?.Dispose();
            disposables.Dispose();
        }
    }
}
