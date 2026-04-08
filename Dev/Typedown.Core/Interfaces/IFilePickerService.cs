using System.Threading.Tasks;

namespace Typedown.Core.Interfaces
{
    /// <summary>
    /// Cross-platform file picker service to replace WinUI FileOpenPicker/FileSavePicker.
    /// </summary>
    public interface IFilePickerService
    {
        Task<string> PickOpenFileAsync(string[] fileTypes, string title = null);
        Task<string> PickSaveFileAsync(string defaultName, string[] fileTypes, string title = null);
        Task<string> PickFolderAsync(string title = null);
    }
}
