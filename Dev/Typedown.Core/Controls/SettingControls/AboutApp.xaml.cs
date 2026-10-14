using Typedown.Core.Utilities;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class AboutApp : UserControl
    {
        public AboutApp()
        {
            InitializeComponent();
        }

        // From Typedown.Automation.Brand: the edition's own link and feedback place.
        public static string LinkText => Typedown.Automation.Brand.AboutLinkText;
        public static System.Uri LinkUrl => new(Typedown.Automation.Brand.AboutLinkUrl);

        public static string GetAppVersion()
        {
            string version;
            if (Config.IsPackaged)
            {
                var packageVersion = Package.Current.Id.Version;
                version = string.Format("{0}.{1}.{2}.{3}", packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
                return string.IsNullOrWhiteSpace(Config.TestBuild) ? version : version + " (" + Config.TestBuild + ")";
            }

            // The entry assembly is Typedown.exe, whose version comes from the release number in its csproj;
            // use it here so an unpackaged build reports the application version rather than the library version.
            var assemblyVersion = (System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly()).GetName().Version;
            version = string.Format("{0}.{1}.{2}.{3}", assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build, assemblyVersion.Revision);
            var buildKind = string.IsNullOrWhiteSpace(Config.TestBuild) ? "Unpackaged" : Config.TestBuild + ", Unpackaged";
            return version + " (" + buildKind + ")";
        }

        /// <summary>
        /// The ways to give feedback an edition offers instead of the link (set from Edition.Initialize): Send feedback
        /// opens them as a menu. Typedown leaves it null and the button opens its GitHub issues.
        /// </summary>
        public static System.Func<ViewModels.AppViewModel, System.Collections.Generic.IEnumerable<MenuFlyoutItemBase>> FeedbackItems { get; set; }

        private void FeedBackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var items = FeedbackItems?.Invoke(this.GetService<ViewModels.AppViewModel>());
                if (items != null)
                {
                    var menu = new MenuFlyout();
                    foreach (var item in items) menu.Items.Add(item);
                    if (menu.Items.Count > 0)
                    {
                        menu.ShowAt(FeedBackButton);
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Utilities.Log.WriteLocal("AboutFeedback", ex.ToString());
            }
            // Feedback goes to the edition's GitHub issues, where it reaches the people who build it, rather
            // than the original author's server that the in-app dialog posted to.
            Utilities.Common.OpenUrl(Typedown.Automation.Brand.FeedbackUrl);
        }

        private void OpenLogFolderButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Utilities.Log.LogFolder);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{Utilities.Log.LogFolder}\"");
            }
            catch (System.Exception ex)
            {
                Utilities.Log.Debug($"about: could not open the log folder: {ex.Message}");
            }
        }

        /// <summary>
        /// A section of its own that an edition adds below the rest (set from Edition.Initialize); called each time the
        /// page is shown. Typedown leaves it null and the page is as it always was.
        /// </summary>
        public static System.Func<UIElement> EditionSection { get; set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                EditionSectionPresenter.Content = EditionSection?.Invoke();
            }
            catch (System.Exception ex)
            {
                Utilities.Log.WriteLocal("AboutEditionSection", ex.ToString());
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            EditionSectionPresenter.Content = null;
            Bindings?.StopTracking();
        }
    }
}
