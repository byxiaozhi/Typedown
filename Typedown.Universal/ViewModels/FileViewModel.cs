using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Windows.ApplicationModel.Resources;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.ViewModels
{
    public class FileViewModel : ObservableObject, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public SettingsViewModel Settings => ServiceProvider.GetService<SettingsViewModel>();

        public EditorViewModel Editor => ServiceProvider.GetService<EditorViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public WorkFolder WorkFolder => ServiceProvider.GetService<WorkFolder>();

        public string FilePath { get; set; } = null;

        public string FileName => Path.GetFileName(FilePath);

        public Command<Unit> NewFile { get; } = new();
        public Command<string> NewWindow { get; } = new();
        public Command<Unit> OpenFile { get; } = new();
        public Command<Unit> OpenFolder { get; } = new();
        public Command<Unit> NewFolder { get; } = new();
        public Command<Unit> ClearHistory { get; } = new();
        public Command<Unit> Save { get; } = new();
        public Command<Unit> SaveAs { get; } = new();
        public Command<Unit> Import { get; } = new();
        public Command<string> Export { get; } = new();
        public Command<Unit> Print { get; } = new();
        public Command<Unit> Exit { get; } = new();

        private readonly ResourceLoader dialogMessages = ResourceLoader.GetForViewIndependentUse("DialogMessages");

        private readonly DispatcherTimer saveFileTimer = new();

        public AutoBackup AutoBackup => ServiceProvider.GetService<AutoBackup>();

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

        public FileViewModel(IServiceProvider serviceProvider)
        {
            
        }

        public async void SaveFileTimer_Tick(object sender, object e)
        {
            if (Settings.AutoSave)
            {
                Editor.AutoSavedSucc = await AutoSaveFile();
                if (!Editor.AutoSavedSucc)
                    await AutoBackupFile();
            }
            else
            {
                await AutoBackupFile();
            }
        }

        public async Task<bool> AutoSaveFile()
        {
            if (Settings.AutoSave && (Editor.FileHash != Editor.CurrentHash) && FilePath != null)
                return await SaveFun(false);
            return FilePath != null;
        }

        public async Task<bool> AutoBackupFile()
        {
            if ((Editor.FileHash != Editor.CurrentHash))
                return await AutoBackup.Backup(FilePath, Editor.Markdown);
            return true;
        }

        private async Task NewFileFun(bool postMessage = true)
        {
            if (!await AskToSave()) return;
            FilePath = null;
            Editor.FileHash = Common.SimpleHash(Common.DefaultMarkdwn);
            var backup = await CheckBackup(FilePath, Editor.FileHash);
            if (backup == null)
            {
                Editor.Markdown = Common.DefaultMarkdwn;
                Editor.CurrentHash = Editor.FileHash;
                Editor.Saved = true;
                Editor.AutoSavedSucc = true;
                Editor.FileLoaded = false;
            }
            else
            {
                Editor.Markdown = backup;
                Editor.CurrentHash = Common.SimpleHash(backup);
                Editor.Saved = false;
                Editor.AutoSavedSucc = false;
                Editor.FileLoaded = true;
            }
            Editor.History.InitHistory(Common.DefaultMarkdwn);
            if (postMessage)
            {
                Transport?.PostMessage("LoadFile", Editor.Markdown);
            }
        }

        public async void TryOpenFile(string filepath)
        {
            if (!await AskToSave()) return;
            await LoadFile(filepath, true);
        }

        private async void OpenFileFun()
        {
            if (!await AskToSave()) return;
            var filePicker = new FileOpenPicker();
            FileExtension.Markdown.ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(ViewModel.MainWindow);
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFile(file.Path, true);
            }
        }

        private async void OpenFolderFun()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SetOwnerWindow(ViewModel.MainWindow);
            FileExtension.Markdown.ForEach(folderPicker.FileTypeFilter.Add);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Settings.WorkFolder = folder.Path;
                if (!Settings.IsSideBarOpen)
                {
                    Settings.IsSideBarOpen = true;
                }
                if (Settings.SideBarIndex != 0)
                {
                    Settings.SideBarIndex = 0;
                }
            }
        }

        public async Task<string> CheckBackup(string path, ulong fileHash)
        {
            string text = await AutoBackup.GetBackup(path);
            if (text == null || Common.SimpleHash(text) == fileHash) return null;
            var dialog = AppContentDialog.Create();
            dialog.Title = dialogMessages.GetString("RecoverTitle");
            dialog.Content = dialogMessages.GetString("RecoverContent");
            dialog.PrimaryButtonText = dialogMessages.GetString("Recover");
            dialog.SecondaryButtonText = dialogMessages.GetString("Delete");
            dialog.DefaultButton = ContentDialogButton.Primary;
            var result = await dialog.ShowAsync(ViewModel.XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                return text;
            }
            else
            {
                AutoBackup.DeleteBackup(path);
                return null;
            }
        }

        public async Task LoadFile(string path, bool skipSavedCheck = false, bool postMessage = true)
        {
            throw new NotImplementedException();
            //try
            //{
            //    var openedWindow = AppViewModel.InstanceList.Where(x => x.FileViewModel.FilePath == path).FirstOrDefault();
            //    if (openedWindow != default && openedWindow.FileViewModel != this)
            //    {
            //        FolderItemModel.UpdateSelectedItem(this);
            //        openedWindow.MainWindow.Activate();
            //        return;
            //    }
            //    if (!File.Exists(path))
            //    {
            //        if (Settings.History.Contains(path))
            //        {
            //            Settings.History = Settings.History.Where(x => x != path).ToList();
            //        }
            //        throw new FileNotFoundException("File Not Found");
            //    }
            //    else if (!skipSavedCheck && !await AskToSave())
            //    {
            //        FolderItemModel.UpdateSelectedItem(this);
            //        return;
            //    }
            //    var text = await File.ReadAllTextAsync(path);
            //    Editor.FirstStart = false;
            //    Editor.FileHash = Common.SimpleHash(text);
            //    FilePath = path;
            //    RecordHistory(FilePath);
            //    var backup = await CheckBackup(path, Editor.CurrentHash);
            //    if (backup == null)
            //    {
            //        Editor.Markdown = text;
            //        Editor.CurrentHash = Editor.FileHash;
            //        Editor.Saved = true;
            //        Editor.FileLoaded = false;
            //    }
            //    else
            //    {
            //        Editor.Markdown = backup;
            //        Editor.CurrentHash = Common.SimpleHash(backup);
            //        Editor.Saved = false;
            //        Editor.FileLoaded = true;
            //    }
            //    Editor.AutoSavedSucc = true;
            //    Editor.History.InitHistory(Editor.Markdown);
            //    if (postMessage)
            //    {

            //        Transport?.PostMessage("LoadFile", Editor.Markdown);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    FolderItemModel.UpdateSelectedItem(this);
            //    await AppContentDialog.Create(dialogMessages.GetString("ReadErrorTitle"), ex.Message, dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
            //}
        }

        public void RecordHistory(string path)
        {
            var history = Settings.History;
            history.Remove(path);
            history.Insert(0, path);
            Settings.History = history.Take(10).ToList();
        }

        private void ClearHistoryFun()
        {
            Settings.History = new List<string>();
        }

        public async Task<bool> SaveFun(bool alert = true)
        {
            if (FilePath == null)
            {
                var result = await SaveAsFun();
                return result != null;
            }
            else
            {
                var result = (bool)await WriteAllText(JObject.FromObject(new { path = FilePath, text = Editor.Markdown, alert }));
                if (result)
                {
                    Editor.FileHash = Editor.CurrentHash;
                    Editor.Saved = true;
                    RecordHistory(FilePath);
                }
                return result;
            }
        }

        private async Task<string> SaveAsFun()
        {
            var filePicker = new FileSavePicker();
            filePicker.SetOwnerWindow(ViewModel.MainWindow);
            filePicker.FileTypeChoices.Add("Markdown Files", FileExtension.Markdown);
            filePicker.SuggestedFileName = FileName ?? "untitled";
            var file = await filePicker.PickSaveFileAsync();
            if (file != null)
            {
                var result = (bool)await WriteAllText(JObject.FromObject(new { path = file.Path, text = Editor.Markdown }));
                if (result)
                {
                    if (FilePath == null)
                    {
                        FilePath = file.Path;
                        AutoBackup.DeleteBackup(null);
                    }
                    Editor.FileHash = Editor.CurrentHash;
                    Editor.Saved = true;
                    RecordHistory(FilePath);
                    return file.Path;
                }
            }
            return null;
        }

        private async Task<object> ConvertHTML(JToken arg)
        {
            var html = arg["html"].ToString();
            var format = arg["format"].ToString();
            var path = arg["path"].ToString();
            try
            {
                if (format == "pdf")
                {
                    var fileExport = ServiceProvider.GetService<IFileExport>();
                    fileExport.HtmlToPdf(Path.GetDirectoryName(FilePath), html, FilePath, path);
                }
                return true;
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(dialogMessages.GetString("ExportErrorTitle"), ex.Message, dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
                return false;
            }
        }

        private object PrintHTML(JToken arg)
        {
            var html = arg["html"].ToString();
            var fileExport = ServiceProvider.GetService<IFileExport>();
            fileExport.Print(Path.GetDirectoryName(FilePath), html);
            return true;
        }

        private async Task<object> WriteAllText(JToken arg)
        {
            try
            {
                await File.WriteAllTextAsync(arg["path"].ToString(), arg["text"].ToString());
                return true;
            }
            catch (Exception ex)
            {
                if (arg["alert"] == null || arg["alert"].ToObject<bool>())
                {
                    await AppContentDialog.Create(dialogMessages.GetString("SaveErrorTitle"), ex.Message, dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
                }
                return false;
            }
        }

        private bool askToSaveOpened;

        public async Task<bool> AskToSave()
        {
            if (Editor.Saved || (Settings.AutoSave && await AutoSaveFile()))
            {
                return true;
            }
            if (askToSaveOpened)
            {
                return false;
            }
            askToSaveOpened = true;
            var result = await AppContentDialog.Create(
                dialogMessages.GetString("AsKToSaveTitle"),
                dialogMessages.GetString("AsKToSaveContent"),
                dialogMessages.GetString("Cancel"),
                dialogMessages.GetString("Save"),
                dialogMessages.GetString("Don'tSave")).ShowAsync(ViewModel.XamlRoot);
            askToSaveOpened = false;
            switch (result)
            {
                case ContentDialogResult.Primary:
                    var saveResult = await SaveFun();
                    return saveResult;
                case ContentDialogResult.Secondary:
                    AutoBackup.DeleteBackup(FilePath);
                    return true;
                case ContentDialogResult.None:
                    return false;
            }
            return false;
        }

        public async Task LoadStartUpMarkdown()
        {
            string[] args = Environment.GetCommandLineArgs();
            var path = FilePath ?? args.Where(x => FileExtension.Markdown.Where(x.EndsWith).Any()).FirstOrDefault();
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
                await NewFileFun(false);
            }
        }

        private async void ExportFun(string type)
        {
            var filePicker = new FileSavePicker();
            filePicker.SetOwnerWindow(ViewModel.MainWindow);
            filePicker.FileTypeChoices.Add(type, new List<string> { "." + type });
            var file = await filePicker.PickSaveFileAsync();
            if (file == null) return;
            Transport?.PostMessage("Export", new { type, path = file.Path, title = file.DisplayName });
        }

        private void PrintFun()
        {
            Transport?.PostMessage("Export", new { type = "print", title = FileName ?? "untitled" });
        }

        private async void ImportFun()
        {
            try
            {
                var filePicker = new FileOpenPicker();
                filePicker.FileTypeFilter.Add(".html");
                var file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    var text = await File.ReadAllTextAsync(file.Path);
                    Transport?.PostMessage("ImportFile", new { type = Path.GetExtension(file.Path).Substring(1), text });
                }
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(dialogMessages.GetString("ImportErrorTitle"), ex.Message, dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
            }
        }

        public void Dispose()
        {
            saveFileTimer.Stop();
        }

        private void ExitFun()
        {
            throw new NotImplementedException();
        }
    }
}
