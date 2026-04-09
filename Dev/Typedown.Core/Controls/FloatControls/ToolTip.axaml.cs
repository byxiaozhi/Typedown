using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class ToolTip : UserControl
    {
        public static readonly StyledProperty<double> FlyoutOpacityProperty = AvaloniaProperty.Register<ToolTip, double>(nameof(FlyoutOpacity), 0d);
        public double FlyoutOpacity { get => GetValue(FlyoutOpacityProperty); set => SetValue(FlyoutOpacityProperty, value); }

        public AppViewModel ViewModel { get; }

        public IMarkdownEditor MarkdownEditor { get; }

        private readonly Flyout flyout = new();

        private bool isOpen;

        private readonly Avalonia.Threading.DispatcherTimer hideTimer = new();

        public ToolTip(AppViewModel viewModel, IMarkdownEditor markdownEditor)
        {
            ViewModel = viewModel;
            MarkdownEditor = markdownEditor;
            hideTimer.Interval = TimeSpan.FromSeconds(10);
            hideTimer.Tick += OnHideTimerTick;
            flyout.Closed += OnFlyoutClosed;
            InitializeComponent();
        }

        public async void Open(string text)
        {
            if (isOpen) return;
            isOpen = true;
            await Task.Delay(1000);
            if (!isOpen) return;
            BeginInAnimation();
            var editor = (Control)MarkdownEditor;
            // ContentTextBlock.Text = text; // Need to resolve control references properly in avalonia code behind usually via this.FindControl<TextBlock> or just let x:Name bind.
            
            // flyout.AreOpenCloseAnimationsEnabled = false;
            // flyout.OverlayInputPassThroughElement = ViewModel.XamlRoot.Content;
            // flyout.FlyoutPresenterStyle = Resources["ToolTipFlyoutStyle"] as Style;
            flyout.Content = this;
            flyout.ShowAt(editor, true);
            hideTimer.Start();
        }

        private void OnFlyoutClosed(object sender, object e)
        {
            hideTimer.Stop();
            isOpen = false;
        }

        private void OnHideTimerTick(object sender, object e)
        {
            Hide();
            hideTimer.Stop();
        }

        public async void Hide()
        {
            isOpen = false;
            BeginOutAnimation();
            await Task.Delay(200); // 200 ms for duration
            flyout.Hide();
        }

        public void BeginInAnimation()
        {
            // Avalonia animation trigger
        }

        public void BeginOutAnimation()
        {
            // Avalonia animation trigger
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             // Bindings?.StopTracking();
        }
    }
}

