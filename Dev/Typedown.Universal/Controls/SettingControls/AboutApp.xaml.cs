using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class AboutApp : UserControl
    {
        public AboutApp()
        {
            InitializeComponent();
        }

        public static string GetAppVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
            catch
            {
                return "Unpackaged";
            }
        }

        private async void FeedBackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await FeedbackDialog.OpenFeedbackDialog(XamlRoot);
        }
    }
}
