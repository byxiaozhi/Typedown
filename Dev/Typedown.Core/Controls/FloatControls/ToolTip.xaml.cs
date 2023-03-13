using System;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class ToolTip : UserControl
    {
        public static DependencyProperty FlyoutOpacityProperty = DependencyProperty.Register(nameof(FlyoutOpacity), typeof(double), typeof(ToolTip), new(0d));
        public double FlyoutOpacity { get => (double)GetValue(FlyoutOpacityProperty); set => SetValue(FlyoutOpacityProperty, value); }

        public AppViewModel ViewModel { get; }

        public IMarkdownEditor MarkdownEditor { get; }

        private readonly Flyout flyout = new();

        private bool isOpen;

        private readonly DispatcherTimer hideTimer = new();

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
            var editor = (MarkdownEditor as FrameworkElement);
            var pos = editor.GetService<IWindowService>().GetCursorPos(editor);
            pos.Y -= 16;
            ContentTextBlock.Text = text;
            flyout.AreOpenCloseAnimationsEnabled = false;
            flyout.OverlayInputPassThroughElement = ViewModel.XamlRoot.Content;
            flyout.FlyoutPresenterStyle = Resources["ToolTipFlyoutStyle"] as Style;
            flyout.Content = this;
            flyout.ShowAt(editor, new() { Placement = FlyoutPlacementMode.Top, ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway, Position = pos });
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
            await Task.Delay((int)OpacityAnimation.Duration.TimeSpan.TotalMilliseconds);
            flyout.Hide();
        }

        public void BeginInAnimation()
        {
            OpacityAnimation.To = 1;
            OpacityStoryboard.Begin();
        }

        public void BeginOutAnimation()
        {
            OpacityAnimation.To = 0;
            OpacityStoryboard.Begin();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
