using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.System;

namespace Typedown.WinUI
{
    // New since the fork: the Help menu. How the features work is written down once, in Markdown files that ship with the app
    // (Help/<language>/<topic>.md, English where a language has none), and opened like a template: as a new document, a copy the
    // user may keep or close. Nothing is fetched from the internet; "Report a problem" only opens the project's page in the browser.
    public sealed partial class MainWindow
    {
        private const string IssuesUrl = "https://github.com/fegyenc/Caret/issues";

        private async void HelpTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string topic }) return;
            try
            {
                var content = (await TextFileEncoding.ReadAsync(HelpTopics.PathOf(AppContext.BaseDirectory, Locale.CurrentLang, topic))).Text;
                if (!await MakeRoomForDocument()) return;
                file.NewFile();
                file.ApplyRecoveredBackup(content);
                UpdateTitle();
                Log($"Help: opened topic {topic}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntOpenHelp"), ex.Message);
            }
        }

        private async void HelpReportProblem_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(new Uri(IssuesUrl));

        // Settings > About: the version, the licence and the project.
        private void HelpAbout_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingsPageShown) ShowSettingsPage();
            SettingsNavList.SelectedItem = SettingsNavItems.First(i => i.Tag as string == "About");
        }
    }
}
