﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Typedown.Core.Converters
{
    public class EmptyToVisibilityConverter : IValueConverter
    {
        public bool IsReverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var res = string.IsNullOrEmpty(value as string);
            if (IsReverse) res = !res;
            return res ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var res = (Visibility)value;
            return IsReverse ? res == Visibility.Collapsed : res == Visibility.Visible;
        }
    }
}