﻿using System;
using System.Reflection;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Typedown.Core.Controls
{
    public class EnumNameBlock : ContentControl
    {
        public static DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object), typeof(EnumNameBlock), null);
        public object Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public EnumNameBlock()
        {
            var textBlock = new TextBlock();
            Content = textBlock;
            textBlock.SetBinding(TextBlock.TextProperty, new Binding() { Source = this, Path = new(nameof(Value)), Converter = new ValueConverter() });
        }

        private class ValueConverter: IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, string language)
            {
                var field = value?.GetType().GetField(value.ToString());
                var attribute = field?.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
                return attribute?.Text;
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                throw new NotImplementedException();
            }
        }
    }
}
