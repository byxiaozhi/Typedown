using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Core.Controls
{
    public sealed partial class ProgressButton : Button
    {
        public static DependencyProperty IsLoadingProperty { get; } = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ProgressButton), new(false, (d, e) => (d as ProgressButton).OnDependencyPropertyChanged(e)));
        public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

        public static new DependencyProperty IsEnabledProperty { get; } = DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(ProgressButton), new(true, (d, e) => (d as ProgressButton).OnDependencyPropertyChanged(e)));
        public new bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

        public static new DependencyProperty ContentProperty { get; } = DependencyProperty.Register(nameof(Content), typeof(object), typeof(ProgressButton), new(null));
        public new object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public ProgressButton()
        {
            InitializeComponent();
        }

        private void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.IsEnabled = IsEnabled && !IsLoading;
        }
    }
}
