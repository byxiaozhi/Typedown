using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Controls;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Core.Pages.SettingPages
{
    [Locale("ExportConfig.Title")]
    public sealed partial class ExportConfigPage : Page
    {
        private static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(ExportConfigPage), null);
        private ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public Lazy<IFileExport> ExportService { get; }

        private int configId;

        private readonly CompositeDisposable disposables = new();

        public ExportConfigPage()
        {
            ExportService = new(() => this.GetService<IFileExport>());
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            int.TryParse(e.Parameter.ToString(), out configId);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportConfig = await ExportService.Value.GetExportConfig(configId);
            }
            catch (Exception ex)
            {
                // The database could not be read: the page stays empty rather than taking the window down.
                Log.WriteLocal("ExportConfigLoad", ex.ToString());
                return;
            }
            if (ExportConfig != null)
                disposables.Add(ExportConfig.WhenPropertyChanged(nameof(ExportConfig.Name)).Cast<string>().StartWith(ExportConfig.Name).Subscribe(UpdateTitle));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // The window's own root: this page is gone by the time the save answers.
            var root = this.GetService<AppViewModel>()?.XamlRoot;
            _ = Dispatcher.RunIdleAsync(async () =>
            {
                // A configuration that could not be saved (the database locked) says so: the edits were lost silently.
                if (ExportConfig != null && !await ExportService.Value.SaveExportConfig(ExportConfig) && root != null)
                    await AppContentDialog.Create(Locale.GetDialogString("SaveErrorTitle"), ExportConfig.Name, Locale.GetString("Ok")).ShowAsync(root);
            });
            disposables.Clear();
            Bindings?.StopTracking();
        }

        private void UpdateTitle(string title)
        {
            this.GetAncestor<SettingsPage>()?.SetPageTitle(this, title);
        }

        public FrameworkElement GetExportConfigItem(ExportType type)
        {
            return type switch
            {
                ExportType.PDF => new PDFConfig() { ExportConfig = ExportConfig },
                ExportType.HTML => new HTMLConfig() { ExportConfig = ExportConfig },
                ExportType.Image => new ImageConfig() { ExportConfig = ExportConfig },
                _ => null
            };
        }

        public async Task DeleteConfigAsync()
        {
            if (ExportConfig != null)
            {
                var service = this.GetService<IFileExport>();
                await service.RemoveExportConfig(ExportConfig.Id);
                ExportConfig = null;
                Frame.GoBack();
            }
        }
    }
}
