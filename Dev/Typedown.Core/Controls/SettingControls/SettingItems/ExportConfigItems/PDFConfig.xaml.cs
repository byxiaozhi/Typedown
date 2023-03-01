using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using Typedown.Core.Models;
using Typedown.Core.Models.ExportConfigModels;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    public sealed partial class PDFConfig : UserControl, INotifyPropertyChanged
    {
        public static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(PDFConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static DependencyProperty PDFConfigModelProperty { get; } = DependencyProperty.Register(nameof(ImageConfigModel), typeof(PDFConfigModel), typeof(PDFConfig), null);
        public PDFConfigModel PDFConfigModel { get => (PDFConfigModel)GetValue(PDFConfigModelProperty); set => SetValue(PDFConfigModelProperty, value); }

        public ObservableCollection<PDFConfigPageSizeItem> PageSizeComboxItems { get; set; }

        public PDFConfigPageSizeItem PageSizeComboxSelectedItem { get; set; }

        public ObservableCollection<PDFConfigPageMarginItem> PageMarginComboxItems { get; set; }

        public PDFConfigPageMarginItem PageMarginComboxSelectedItem { get; set; }

        private readonly CompositeDisposable disposables = new();

        public PDFConfig()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PDFConfigModel = ExportConfig.LoadExportConfig() as PDFConfigModel;
            PageSizeComboxItems = new(PageSize.StandardPageSizes.Select(x => new PDFConfigPageSizeItem() { Name = x.Name, PageSize = x.PageSize }));
            PageMarginComboxItems = new(PageMargin.StandardPageMargin.Select(x => new PDFConfigPageMarginItem() { Name = x.Name, PageMargin = x.PageMargin }));
            disposables.Add(PDFConfigModel.PageSize.GetPropertyObservable().Subscribe(_ => OnPageSizeChanged()));
            disposables.Add(PDFConfigModel.Margins.GetPropertyObservable().Subscribe(_ => OnPageMarginChanged()));
            OnPageSizeChanged();
            OnPageMarginChanged();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExportConfig.StoreExportConfig(PDFConfigModel);
            disposables.Clear();
            Bindings.StopTracking();
        }

        private void OnPageSizeComboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ((sender as ComboBox).SelectedItem as PDFConfigPageSizeItem);
            if (selected?.PageSize != null)
            {
                PDFConfigModel.PageSize.Width = selected.PageSize.Width;
                PDFConfigModel.PageSize.Height = selected.PageSize.Height;
            }
        }

        private void OnPageMarginComboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ((sender as ComboBox).SelectedItem as PDFConfigPageMarginItem);
            if (selected?.PageMargin != null)
            {
                PDFConfigModel.Margins.Left = selected.PageMargin.Left;
                PDFConfigModel.Margins.Top = selected.PageMargin.Top;
                PDFConfigModel.Margins.Right = selected.PageMargin.Right;
                PDFConfigModel.Margins.Bottom = selected.PageMargin.Bottom;
            }
        }

        private void OnPageSizeChanged()
        {
            var customItem = PageSizeComboxItems.Where(x => x.PageSize == null).FirstOrDefault();
            var selectItem = PageSizeComboxItems.Where(x => x.PageSize != null && x.PageSize.ApproxEquals(PDFConfigModel.PageSize)).FirstOrDefault();
            if (selectItem == null)
            {
                if (customItem == null)
                    PageSizeComboxItems.Add(selectItem = new() { Name = Locale.GetString("Custom") });
                else
                    selectItem = customItem;
            }
            else
            {
                if (customItem != null)
                    PageSizeComboxItems.Remove(customItem);
            }
            PageSizeComboxSelectedItem = selectItem;
        }

        private void OnPageMarginChanged()
        {
            var customItem = PageMarginComboxItems.Where(x => x.PageMargin == null).FirstOrDefault();
            var selectItem = PageMarginComboxItems.Where(x => x.PageMargin != null && x.PageMargin.ApproxEquals(PDFConfigModel.Margins)).FirstOrDefault();
            if (selectItem == null)
            {
                if (customItem == null)
                    PageMarginComboxItems.Add(selectItem = new() { Name = Locale.GetString("Custom") });
                else
                    selectItem = customItem;
            }
            else
            {
                if (customItem != null)
                    PageMarginComboxItems.Remove(customItem);
            }
            PageMarginComboxSelectedItem = selectItem;
        }
    }

    public class PDFConfigPageSizeItem
    {
        public string Name { get; set; }

        public PageSize PageSize { get; set; }
    }

    public class PDFConfigPageMarginItem
    {
        public string Name { get; set; }

        public PageMargin PageMargin { get; set; }
    }
}
