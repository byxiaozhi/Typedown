using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Typedown.WinUI.Models
{
    // One row in the Convert to Markdown page (MainWindow.Convert.cs). PropertyChanged.Fody raises the
    // change notifications, so the row updates as the conversion finishes.
    public sealed class ConversionItem : INotifyPropertyChanged
    {
#pragma warning disable CS0067 // raised by PropertyChanged.Fody
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public string SourcePath { get; set; }
        public string Name { get; set; }
        public string OutputPath { get; set; }
        public string Markdown { get; set; }
        public string Detail { get; set; }
        public bool IsConverting { get; set; }
        [PropertyChanged.DependsOn(nameof(IsConverting))]
        public Visibility ProgressVisibility => IsConverting ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ActionsVisibility { get; set; } = Visibility.Collapsed;
        public long SourceBytes { get; set; }
        public long MarkdownBytes { get; set; }
        public int Tokens { get; set; }
        public bool Succeeded { get; set; }
        public bool Failed { get; set; }

        // The coloured file-type badge: the Office app colours people already recognise.
        public string Badge { get; set; }
        public SolidColorBrush BadgeBrush { get; set; } = new(Colors.Gray);

        public static (string Badge, Windows.UI.Color Color) BadgeFor(string extension) => extension?.ToLowerInvariant() switch
        {
            ".docx" or ".docm" or ".dotx" or ".doc" => ("DOCX", Windows.UI.Color.FromArgb(255, 0x2B, 0x57, 0x9A)),
            ".xlsx" or ".xlsm" or ".xls" => ("XLSX", Windows.UI.Color.FromArgb(255, 0x21, 0x73, 0x46)),
            ".csv" => ("CSV", Windows.UI.Color.FromArgb(255, 0x21, 0x73, 0x46)),
            ".pptx" or ".pptm" or ".ppt" => ("PPTX", Windows.UI.Color.FromArgb(255, 0xC4, 0x3E, 0x1C)),
            ".pdf" => ("PDF", Windows.UI.Color.FromArgb(255, 0xB3, 0x0B, 0x00)),
            ".msg" => ("MSG", Windows.UI.Color.FromArgb(255, 0x00, 0x5A, 0x9E)),
            ".eml" => ("EML", Windows.UI.Color.FromArgb(255, 0x00, 0x5A, 0x9E)),
            _ => ((extension ?? "").TrimStart('.').ToUpperInvariant(), Windows.UI.Color.FromArgb(255, 0x6B, 0x6B, 0x6B)),
        };
    }
}
