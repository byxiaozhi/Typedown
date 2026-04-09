using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Reflection;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls;

namespace Typedown.Core.Controls
{
    public class EnumNameBlock : ContentControl
    {
        public static AvaloniaProperty ValueProperty = AvaloniaProperty.Register<EnumNameBlock, object>(nameof(Value), null);
        public object Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public EnumNameBlock()
        {
            var textBlock = new TextBlock();
            Content = textBlock;
            textBlock[!TextBlock.TextProperty] = new Avalonia.Data.Binding() { Source = this, Path = nameof(Value), Converter = new ValueConverter() };
        }

        private class ValueConverter: IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var field = value?.GetType().GetField(value.ToString());
                var attribute = field?.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
                return attribute?.Text ?? value?.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}

