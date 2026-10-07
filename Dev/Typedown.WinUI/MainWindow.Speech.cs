using Microsoft.UI.Xaml;

namespace Typedown.WinUI
{
    // New since the fork: speech marks (docs/speech-marks-design.md), delivery marks for a talk written in the text
    // itself: {pause 2s}, {slow}...{/slow}, {tone: dry irony}...{/tone}. The editor reads and draws them
    // (Typedown.Editor, Muya/lib/parser/speech.js) only while Speech mode is on. This is the switch: View > Speech mode.
    // It is a setting like Split, off by default, and opening a file never turns it on.
    public sealed partial class MainWindow
    {
        private void SpeechModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.SpeechMode = SpeechModeMenuItem.IsChecked;
            Log($"SpeechMode: {settings.SpeechMode}");
        }
    }
}
