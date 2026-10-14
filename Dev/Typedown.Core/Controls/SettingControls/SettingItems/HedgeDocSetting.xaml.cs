using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class HedgeDocSetting : UserControl, INotifyPropertyChanged
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public event PropertyChangedEventHandler PropertyChanged;

        private string testStatus = "";
        public string TestStatus { get => testStatus; private set { testStatus = value; OnPropertyChanged(); } }

        private bool isTesting;
        public bool IsTesting { get => isTesting; private set { isTesting = value; OnPropertyChanged(); } }

        public HedgeDocSetting()
        {
            InitializeComponent();
        }

        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            var settings = Settings;
            if (settings == null) return;
            IsTesting = true;
            TestStatus = "…";
            try
            {
                TestStatus = await HedgeDocService.TestAsync(settings.HedgeDocServer, settings.HedgeDocEmail, settings.HedgeDocPassword);
            }
            catch (Exception ex)
            {
                TestStatus = ex.Message;
            }
            finally
            {
                IsTesting = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
