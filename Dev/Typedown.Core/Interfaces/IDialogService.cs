using System;
using System.Threading.Tasks;

namespace Typedown.Core.Interfaces
{
    /// <summary>
    /// Cross-platform dialog service to replace AppContentDialog / ContentDialog.
    /// </summary>
    public enum DialogResult { None, Primary, Secondary }

    public interface IDialogService
    {
        Task<DialogResult> ShowAsync(string title, string content, string closeText,
            string primaryText = null, string secondaryText = null);
    }
}
