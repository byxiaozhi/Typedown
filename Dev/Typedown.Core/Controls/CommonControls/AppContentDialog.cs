using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Typedown.Core.Utilities;
using Avalonia.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Avalonia.VisualTree;




namespace Typedown.Core.Controls
{
    public class AppContentDialog : ContentControl
    {
        public static AvaloniaProperty TitleTemplateProperty { get; } = AvaloniaProperty.Register<AppContentDialog, DataTemplate>(nameof(TitleTemplate), null);
        public DataTemplate TitleTemplate { get => (DataTemplate)GetValue(TitleTemplateProperty); set => SetValue(TitleTemplateProperty, value); }

        public static AvaloniaProperty TitleProperty { get; } = AvaloniaProperty.Register<AppContentDialog, object>(nameof(Title), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static AvaloniaProperty SecondaryButtonTextProperty { get; } = AvaloniaProperty.Register<AppContentDialog, string>(nameof(SecondaryButtonText), null);
        public string SecondaryButtonText { get => (string)GetValue(SecondaryButtonTextProperty); set => SetValue(SecondaryButtonTextProperty, value); }

        public static AvaloniaProperty SecondaryButtonCommandParameterProperty { get; } = AvaloniaProperty.Register<AppContentDialog, object>(nameof(SecondaryButtonCommandParameter), null);
        public object SecondaryButtonCommandParameter { get => GetValue(SecondaryButtonCommandParameterProperty); set => SetValue(SecondaryButtonCommandParameterProperty, value); }

        public static AvaloniaProperty SecondaryButtonCommandProperty { get; } = AvaloniaProperty.Register<AppContentDialog, ICommand>(nameof(SecondaryButtonCommand), null);
        public ICommand SecondaryButtonCommand { get => (ICommand)GetValue(SecondaryButtonCommandProperty); set => SetValue(SecondaryButtonCommandProperty, value); }

        public static AvaloniaProperty PrimaryButtonTextProperty { get; } = AvaloniaProperty.Register<AppContentDialog, string>(nameof(PrimaryButtonText), null);
        public string PrimaryButtonText { get => (string)GetValue(PrimaryButtonTextProperty); set => SetValue(PrimaryButtonTextProperty, value); }

        public static AvaloniaProperty PrimaryButtonCommandParameterProperty { get; } = AvaloniaProperty.Register<AppContentDialog, object>(nameof(PrimaryButtonCommandParameter), null);
        public object PrimaryButtonCommandParameter { get => GetValue(PrimaryButtonCommandParameterProperty); set => SetValue(PrimaryButtonCommandParameterProperty, value); }

        public static AvaloniaProperty PrimaryButtonCommandProperty { get; } = AvaloniaProperty.Register<AppContentDialog, ICommand>(nameof(PrimaryButtonCommand), null);
        public ICommand PrimaryButtonCommand { get => (ICommand)GetValue(PrimaryButtonCommandProperty); set => SetValue(PrimaryButtonCommandProperty, value); }

        public static AvaloniaProperty IsSecondaryButtonEnabledProperty { get; } = AvaloniaProperty.Register<AppContentDialog, bool>(nameof(IsSecondaryButtonEnabled), true);
        public bool IsSecondaryButtonEnabled { get => (bool)GetValue(IsSecondaryButtonEnabledProperty); set => SetValue(IsSecondaryButtonEnabledProperty, value); }

        public static AvaloniaProperty IsPrimaryButtonEnabledProperty { get; } = AvaloniaProperty.Register<AppContentDialog, bool>(nameof(IsPrimaryButtonEnabled), true);
        public bool IsPrimaryButtonEnabled { get => (bool)GetValue(IsPrimaryButtonEnabledProperty); set => SetValue(IsPrimaryButtonEnabledProperty, value); }

        public static AvaloniaProperty FullSizeDesiredProperty { get; } = AvaloniaProperty.Register<AppContentDialog, bool>(nameof(FullSizeDesired), false);
        public bool FullSizeDesired { get => (bool)GetValue(FullSizeDesiredProperty); set => SetValue(FullSizeDesiredProperty, value); }

        public static AvaloniaProperty SecondaryButtonStyleProperty { get; } = AvaloniaProperty.Register<AppContentDialog, Style>(nameof(SecondaryButtonStyle), null);
        public Style SecondaryButtonStyle { get => (Style)GetValue(SecondaryButtonStyleProperty); set => SetValue(SecondaryButtonStyleProperty, value); }

        public static AvaloniaProperty PrimaryButtonStyleProperty { get; } = AvaloniaProperty.Register<AppContentDialog, Style>(nameof(PrimaryButtonStyle), null);
        public Style PrimaryButtonStyle { get => (Style)GetValue(PrimaryButtonStyleProperty); set => SetValue(PrimaryButtonStyleProperty, value); }

        public static AvaloniaProperty DefaultButtonProperty { get; } = AvaloniaProperty.Register<AppContentDialog, Typedown.Core.Controls.ContentDialogButton>(nameof(DefaultButton), Typedown.Core.Controls.ContentDialogButton.None);
        public Typedown.Core.Controls.ContentDialogButton DefaultButton { get => (Typedown.Core.Controls.ContentDialogButton)GetValue(DefaultButtonProperty); set => SetValue(DefaultButtonProperty, value); }

        public static AvaloniaProperty CloseButtonTextProperty { get; } = AvaloniaProperty.Register<AppContentDialog, string>(nameof(CloseButtonText), null);
        public string CloseButtonText { get => (string)GetValue(CloseButtonTextProperty); set => SetValue(CloseButtonTextProperty, value); }

        public static AvaloniaProperty CloseButtonStyleProperty { get; } = AvaloniaProperty.Register<AppContentDialog, Style>(nameof(CloseButtonStyle), null);
        public Style CloseButtonStyle { get => (Style)GetValue(CloseButtonStyleProperty); set => SetValue(CloseButtonStyleProperty, value); }

        public static AvaloniaProperty CloseButtonCommandParameterProperty { get; } = AvaloniaProperty.Register<AppContentDialog, object>(nameof(CloseButtonCommandParameter), null);
        public object CloseButtonCommandParameter { get => GetValue(CloseButtonCommandParameterProperty); set => SetValue(CloseButtonCommandParameterProperty, value); }

        public static AvaloniaProperty CloseButtonCommandProperty { get; } = AvaloniaProperty.Register<AppContentDialog, ICommand>(nameof(CloseButtonCommand), null);
        public ICommand CloseButtonCommand { get => (ICommand)GetValue(CloseButtonCommandProperty); set => SetValue(CloseButtonCommandProperty, value); }

        public event EventHandler<AppContentDialog, AppContentDialogClosedEventArgs> Closed;

        public event EventHandler<AppContentDialog, AppContentDialogClosingEventArgs> Closing;

        public event EventHandler<AppContentDialog, AppContentDialogOpenedEventArgs> Opened;

        public event EventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> PrimaryButtonClick;

        public event EventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> SecondaryButtonClick;

        public event EventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> CloseButtonClick;

        public Button PrimaryButton => GetTemplateChild("PrimaryButton") as Button;

        public Button SecondaryButton => GetTemplateChild("SecondaryButton") as Button;

        public Button CloseButton => GetTemplateChild("CloseButton") as Button;

        public Grid LayoutRoot => GetTemplateChild("LayoutRoot") as Grid;

        private Rectangle SmokeLayerBackground => GetTemplateChild("SmokeLayerBackground") as Rectangle;

        private Border BackgroundElement => GetTemplateChild("BackgroundElement") as Border;

        private TaskCompletionSource<ContentDialogResult> result;

        private object prevFocusedElement;

        public AppContentDialog()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            AddHandler(KeyDownEvent, new EventHandler<Avalonia.Input.KeyEventArgs>(OnKeyDown), Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        }

        private Control GetTemplateChild(string name)
        {
            foreach (var child in this.GetVisualDescendants())
            {
                if (child is Control c && c.Name == name) return c;
            }
            return null;
        }

        private void SetButtonState()
        {
            // var primaryVisible = !string.IsNullOrEmpty(PrimaryButtonText);
            // var secondaryVisible = !string.IsNullOrEmpty(SecondaryButtonText);
            // var closeVisible = !string.IsNullOrEmpty(CloseButtonText);
            // Ignore VisualState for now
        }

        private async void SetFocusButton()
        {
            bool success = false;
            prevFocusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            for (int i = 0; i < 10 && !success; i++)
            {
                await Task.Delay(100);
                if (DefaultButton == ContentDialogButton.Primary && PrimaryButton.IsVisible)
                    success = PrimaryButton.Focus();
                else if (DefaultButton == ContentDialogButton.Secondary && SecondaryButton.IsVisible)
                    success = SecondaryButton.Focus();
                else if (DefaultButton == ContentDialogButton.Close && CloseButton.IsVisible)
                    success = CloseButton.Focus();
                else if (PrimaryButton.IsVisible)
                    success = PrimaryButton.Focus();
                else if (SecondaryButton.IsVisible)
                    success = SecondaryButton.Focus();
                else if (CloseButton.IsVisible)
                    success = CloseButton.Focus();
                else
                    success = true;
            }
        }

        public void SetShadow()
        {
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetButtonState();
            SetFocusButton();
            SetShadow();
            // VisualStateManager.GoToState(this, "DialogShowing", true);
            PrimaryButton.Click += OnPrimaryButtonClick;
            SecondaryButton.Click += OnSecondaryButtonClick;
            CloseButton.Click += OnCloseButtonClick;
            Opened?.Invoke(this, new());
            // FocusManager.GettingFocus += OnFocusManagerGettingFocus;
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // VisualStateManager.GoToState(this, "DialogHidden", true);
            PrimaryButton.Click -= OnPrimaryButtonClick;
            SecondaryButton.Click -= OnSecondaryButtonClick;
            CloseButton.Click -= OnCloseButtonClick;
            Closed?.Invoke(this, new(result.Task.Result));
            // FocusManager.GettingFocus -= OnFocusManagerGettingFocus;
            if (prevFocusedElement is Control ele && ele.IsLoaded)
                ele.Focus(); // TryFocusAsync replaced by Focus
        }

        // private void OnFocusManagerGettingFocus(object sender, GettingFocusEventArgs e)
        // {
        //     if (e.NewFocusedElement is FrameworkElement ele)
        //     {
        //         if (ele != this && ele.GetAncestor<AppContentDialog>() == null)
        //         {
        //             if (e.Direction == FocusNavigationDirection.Next)
        //                 e.TrySetNewFocusedElement(this);
        //             else
        //                 e.TryCancel();
        //         }
        //     }
        //     else
        //     {
        //         e.TrySetNewFocusedElement(this);
        //     }
        // }

        private void OnPrimaryButtonClick(object? sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            PrimaryButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.Primary);
        }

        private void OnSecondaryButtonClick(object? sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            SecondaryButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.Secondary);
        }

        private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            CloseButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.None);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (IsLoaded && CloseButton.IsVisible && e.Key == Avalonia.Input.Key.Escape)
                OnCloseButtonClick(this, null!);
        }

        private void SetResult(ContentDialogResult result)
        {
            this.result.SetResult(result);
            var closingEventArgs = new AppContentDialogClosingEventArgs(result);
            Closing?.Invoke(this, closingEventArgs);
            if (closingEventArgs.Cancel)
                return;
            Hide();
        }

        public void Hide()
        {
            if (XamlRoot?.Parent is Grid grid && IsLoaded)
                grid.Children.Remove(this);
        }

        private readonly ConditionalWeakTable<Control, SemaphoreSlim> showSemaphores = new();

        public Control XamlRoot { get; set; }

        public async Task<ContentDialogResult> ShowAsync()
        {
            if (XamlRoot == null || IsLoaded)
                throw new InvalidOperationException();
            if (!showSemaphores.TryGetValue(XamlRoot, out var semaphore))
                showSemaphores.Add(XamlRoot, semaphore = new(1));
            await semaphore.WaitAsync();
            try
            {
                result = new();
                if (XamlRoot.Parent is Panel panel)
                {
                    panel.Children.Add(this);
                }
                return await result.Task;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<ContentDialogResult> ShowAsync(Control xamlRoot)
        {
            XamlRoot = xamlRoot;
            return await ShowAsync();
        }

        public static AppContentDialog Create()
        {
            return new AppContentDialog();
        }

        public static AppContentDialog Create(object content, string closeButtonText)
        {
            var dialog = Create();
            dialog.Content = content;
            dialog.CloseButtonText = closeButtonText;
            dialog.DefaultButton = ContentDialogButton.Close;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText)
        {
            var dialog = Create(content, closeButtonText);
            dialog.Title = title;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText, string primaryButtonText)
        {
            var dialog = Create(title, content, closeButtonText);
            dialog.PrimaryButtonText = primaryButtonText;
            dialog.DefaultButton = ContentDialogButton.Primary;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText, string primaryButtonText, string secondaryButtonText)
        {
            var dialog = Create(title, content, closeButtonText, primaryButtonText);
            dialog.SecondaryButtonText = secondaryButtonText;
            dialog.DefaultButton = ContentDialogButton.Primary;
            return dialog;
        }
    }

    public enum ContentDialogButton
    {
        None = 0,
        Primary,
        Secondary,
        Close
    }

    public enum ContentDialogResult
    {
        None,
        Primary,
        Secondary
    }

    public class AppContentDialogClosedEventArgs
    {
        public ContentDialogResult Result { get; }

        public AppContentDialogClosedEventArgs(ContentDialogResult result)
        {
            Result = result;
        }
    }

    public class AppContentDialogClosingEventArgs
    {
        public bool Cancel { get; set; } = false;

        public ContentDialogResult Result { get; }

        public AppContentDialogClosingEventArgs(ContentDialogResult result)
        {
            Result = result;
        }
    }

    public class AppContentDialogOpenedEventArgs
    {
    }

    public class AppContentDialogButtonClickEventArgs
    {
        public bool Cancel { get; set; } = false;
    }
}

