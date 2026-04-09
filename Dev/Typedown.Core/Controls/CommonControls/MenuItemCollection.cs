using Avalonia.Controls.Primitives;
using System;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Typedown.Core.Controls
{
    public class MenuItemCollection : AvaloniaObject
    {
        public static readonly AttachedProperty<MenuItemCollection> ValueProperty = AvaloniaProperty.RegisterAttached<MenuItemCollection, AvaloniaObject, MenuItemCollection>("Value");
        public static MenuItemCollection GetValue(AvaloniaObject target) => target.GetValue(ValueProperty);
        public static void SetValue(AvaloniaObject target, MenuItemCollection value) => target.SetValue(ValueProperty, value);

        static MenuItemCollection()
        {
            ValueProperty.Changed.Subscribe(e => OnValuePropertyChanged(e.Sender, e));
        }


        private static void OnValuePropertyChanged(AvaloniaObject target, AvaloniaPropertyChangedEventArgs e)
        {
            if (target is MenuItem subItem)
            {
                UpdateMenuItemValue(subItem, e.NewValue as MenuItemCollection);
            }
        }

        private static void UpdateMenuItemValue(MenuItem target, MenuItemCollection collection)
        {
            target.ItemsSource = collection?.Items;
        }

        public static AvaloniaProperty ItemsProperty { get; } = AvaloniaProperty.Register<AvaloniaObject, IList<MenuItem>>(nameof(Items), null);

        [Content]
        public IList<MenuItem> Items { get => (IList<MenuItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        public MenuItemCollection()
        {
            Items = new ObservableCollection<MenuItem>();
        }
    }
}

