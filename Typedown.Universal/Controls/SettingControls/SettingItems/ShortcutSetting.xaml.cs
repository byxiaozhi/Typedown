using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.SettingControls.SettingItems
{
    public sealed partial class ShortcutSetting : UserControl
    {
        private static DependencyProperty SearchTextProperty { get; } = DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(ShortcutSetting), new(string.Empty));
        private string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

        private static DependencyProperty FliterCategoryProperty { get; } = DependencyProperty.Register(nameof(FliterCategory), typeof(ShortcutSettingCategoryModel), typeof(ShortcutSetting), null);
        private ShortcutSettingCategoryModel FliterCategory { get => (ShortcutSettingCategoryModel)GetValue(FliterCategoryProperty); set => SetValue(FliterCategoryProperty, value); }

        private static DependencyProperty FliterCategoriesProperty { get; } = DependencyProperty.Register(nameof(FliterCategories), typeof(List<ShortcutSettingCategoryModel>), typeof(ShortcutSetting), null);
        private List<ShortcutSettingCategoryModel> FliterCategories { get => (List<ShortcutSettingCategoryModel>)GetValue(FliterCategoriesProperty); set => SetValue(FliterCategoriesProperty, value); }

        private List<ShortcutSettingItemModel> AllSettingItems { get; set; }

        private ObservableCollection<ShortcutSettingItemModel> SettingItems { get; } = new();

        private readonly CompositeDisposable disposables = new();

        public ShortcutSetting()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadAllShortcutSettingItems();
            disposables.Add(this.Binding(new(nameof(SearchText))).Merge(this.Binding(new(nameof(FliterCategory))))
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(_ => _ = Dispatcher.RunIdleAsync(_ => UpdateFilteredSettingItems())));
            _ = Dispatcher.RunIdleAsync(_ => UpdateFilteredSettingItems());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        public void LoadAllShortcutSettingItems()
        {
            SettingItems.Clear();
            var target = this.GetService<SettingsViewModel>();
            AllSettingItems = typeof(SettingsViewModel)
                .GetProperties()
                .Where(x => x.PropertyType == typeof(ShortcutKey))
                .Select(x => new ShortcutSettingItemModel(target, x))
                .ToList();
            FliterCategories = AllSettingItems.Select(x => x.Category).ToHashSet().Select(x => new ShortcutSettingCategoryModel(x, x)).ToList();
            FliterCategories.Insert(0, new(Localize.GetString("All"), null));
            FliterCategory = FliterCategories[0];
        }

        public void UpdateFilteredSettingItems()
        {
            var newItems = AllSettingItems
                .Where(x => string.IsNullOrEmpty(FliterCategory?.Category) || x.Category == FliterCategory.Category)
                .Where(x => string.IsNullOrEmpty(SearchText) || x.DisplayName.ToLower().Contains(SearchText.ToLower()) || x.Description.ToLower().Contains(SearchText.ToLower()))
                .ToList();
            SettingItems.UpdateCollection(newItems, (a, b) => a == b);
        }
    }

    public partial class ShortcutSettingItemModel : INotifyPropertyChanged
    {
        public SettingsViewModel Target { get; }

        public PropertyInfo Property { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string Category { get; }

        public ShortcutKey ShortcutKey { get => (ShortcutKey)Property.GetValue(Target); set => Property.SetValue(Target, value); }

        public ShortcutSettingItemModel(SettingsViewModel target, PropertyInfo property)
        {
            Target = target;
            Property = property;
            var texts = Property.GetCustomAttribute<LocalizeAttribute>()?.Texts.ToList();
            if (texts == null || !texts.Any())
            {
                DisplayName = Property.Name;
                Description = "Unknown";
                Category = "Unknown";
            }
            else
            {
                DisplayName = string.IsNullOrEmpty(texts.Last()) ? Property.Name : texts.Last();
                Description = string.Join(" / ", texts.Take(texts.Count - 1).Union(Enumerable.Repeat(DisplayName, 1)));
                Category = texts.First();
            }
        }
    }

    public class ShortcutSettingCategoryModel
    {
        public string DisplayName { get; }

        public string Category { get; }

        public ShortcutSettingCategoryModel(string displayName, string category)
        {
            DisplayName = displayName;
            Category = category;
        }
    }
}
