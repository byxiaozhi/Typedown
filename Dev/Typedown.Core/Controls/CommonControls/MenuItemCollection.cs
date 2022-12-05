using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Typedown.Core.Controls
{
    [ContentProperty(Name = nameof(Items))]
    public class MenuItemCollection : DependencyObject
    {
        public static DependencyProperty ValueProperty { get; } = DependencyProperty.Register("Value", typeof(MenuItemCollection), typeof(MenuItemCollection), new(null, OnValuePropertyChanged));
        public static MenuItemCollection GetValue(DependencyObject target) => (MenuItemCollection)target.GetValue(ValueProperty);
        public static void SetValue(DependencyObject target, MenuItemCollection value) => target.SetValue(ValueProperty, value);

        private static readonly ConditionalWeakTable<DependencyObject, IDisposable> valuePropertyDisposables = new();

        private static void OnValuePropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is MenuFlyoutSubItem subItem)
            {
                UpdateMenuFlyoutSubItemValue(subItem, e.NewValue as MenuItemCollection);
            }
        }

        private static void UpdateMenuFlyoutSubItemValue(MenuFlyoutSubItem target, MenuItemCollection collection)
        {
            if (valuePropertyDisposables.TryGetValue(target, out var disposable))
            {
                disposable.Dispose();
                valuePropertyDisposables.Remove(target);
            }
            if (collection != null)
            {
                var disposables = new CompositeDisposable();
                valuePropertyDisposables.Add(target, disposables);
                disposables.Add(collection.Binding(new(nameof(Items))).Subscribe(_ => UpdateMenuFlyoutSubItemValue(target, collection)));
                if (collection.Items is ObservableCollection<MenuFlyoutItemBase> obsCollection)
                    disposables.Add(obsCollection.GetCollectionObservable().Subscribe(_ => UpdateMenuFlyoutSubItemValue(target, collection)));
                target.Items.UpdateList(collection.Items);
            }
        }

        public static DependencyProperty ItemsProperty { get; } = DependencyProperty.Register(nameof(Items), typeof(IList<MenuFlyoutItemBase>), typeof(DependencyObject), null);

        public IList<MenuFlyoutItemBase> Items { get => (IList<MenuFlyoutItemBase>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        public MenuItemCollection()
        {
            Items = new ObservableCollection<MenuFlyoutItemBase>();
        }
    }
}
