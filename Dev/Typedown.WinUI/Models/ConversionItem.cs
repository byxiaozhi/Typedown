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

        // What a screen reader says for a row of a list.
        public override string ToString() => string.IsNullOrEmpty(Detail) ? Name : Name + ": " + Detail;

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

        // For the conversion tray (MainWindow.Tray.cs): the folder a dropped folder was found under (a retry mirrors the subfolders the same
        // way) and the choices that were applied, which Compare shows next to the two texts.
        public string Root { get; set; }
        public bool ImagesExtracted { get; set; }
        public int ImageCount { get; set; }
        public bool IsEmail { get; set; }
        public bool Masked { get; set; }

        // The tray's row: a progress bar (empty while it waits, running while it converts, full when done or failed), Retry on a failed row.
        [PropertyChanged.DependsOn(nameof(IsConverting), nameof(Succeeded), nameof(Failed))]
        public double ProgressValue => Succeeded || Failed ? 100 : 0;
        [PropertyChanged.DependsOn(nameof(IsConverting))]
        public bool ProgressIndeterminate => IsConverting;
        [PropertyChanged.DependsOn(nameof(Failed))]
        public bool ProgressShowError => Failed;
        [PropertyChanged.DependsOn(nameof(Failed))]
        public Visibility RetryVisibility => Failed ? Visibility.Visible : Visibility.Collapsed;

        // A tinted fill for a running or a failed row; with a Windows contrast theme on, no tint and a 1 px border in the text colour
        // instead (read when the row is made).
        public static bool HighContrast { get; set; }
        [PropertyChanged.DependsOn(nameof(IsConverting))]
        public Visibility ActiveTintVisibility => IsConverting && !HighContrast ? Visibility.Visible : Visibility.Collapsed;
        [PropertyChanged.DependsOn(nameof(Failed))]
        public Visibility FailedTintVisibility => Failed && !HighContrast ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ContrastBorderVisibility => HighContrast ? Visibility.Visible : Visibility.Collapsed;

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
