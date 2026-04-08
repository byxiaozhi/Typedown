using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia implementation of IDialogService using Avalonia's Window dialog system.
/// </summary>
public class AvaloniaDialogService : IDialogService
{
    public async Task<DialogResult> ShowAsync(string title, string content, string closeText,
        string? primaryText = null, string? secondaryText = null)
    {
        var window = GetMainWindow();
        if (window == null)
            return DialogResult.None;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var result = DialogResult.None;

        var contentText = new TextBlock
        {
            Text = content,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(20, 16),
            MaxWidth = 380,
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(20, 0, 20, 16),
        };

        if (primaryText != null)
        {
            var primaryBtn = new Button { Content = primaryText, MinWidth = 80 };
            primaryBtn.Click += (_, _) => { result = DialogResult.Primary; dialog.Close(); };
            buttonPanel.Children.Add(primaryBtn);
        }

        if (secondaryText != null)
        {
            var secondaryBtn = new Button { Content = secondaryText, MinWidth = 80 };
            secondaryBtn.Click += (_, _) => { result = DialogResult.Secondary; dialog.Close(); };
            buttonPanel.Children.Add(secondaryBtn);
        }

        var closeBtn = new Button { Content = closeText, MinWidth = 80 };
        closeBtn.Click += (_, _) => { result = DialogResult.None; dialog.Close(); };
        buttonPanel.Children.Add(closeBtn);

        var root = new DockPanel();
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);
        root.Children.Add(contentText);

        dialog.Content = root;
        await dialog.ShowDialog(window);

        return result;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
