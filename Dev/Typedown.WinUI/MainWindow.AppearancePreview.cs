using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: Settings > Appearance as cards you can see. The colour schemes are cards with a picture of the scheme (a border in the
    // accent colour under the pointer, a thicker one and a check mark on the chosen one, the system's ring on the one the keys are on), a live
    // preview of the window shows the scheme with the section colours as they are now (the rules are the window's: Utilities/AppearancePreview.cs),
    // and every section colour says "Custom" when it is not the scheme's own and has its own "Reset to scheme".
    public sealed partial class MainWindow
    {
        private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

        // The scheme in use, as the preview and the Custom badges read it.
        private AppearancePreview.Scheme SchemeInput(bool dark)
        {
            var p = ColorSchemes.Current(dark);
            return new AppearancePreview.Scheme(p.Primary, p.Background, p.TabStrip, p.Text, p.Text2, p.TabText, p.Border);
        }

        private string StoredSectionColors(bool dark) => dark ? settings.SectionColorsDark : settings.SectionColorsLight;

        // --- The cards ---

        // A card: the picture of the scheme (its tab band, sidebar, page and accent, in the theme that is showing) and its name.
        private FrameworkElement BuildSchemeCard(ColorSchemes.Scheme scheme)
        {
            var p = IsDark ? scheme.Dark : scheme.Light;
            Brush B(string hex) => new SolidColorBrush(ColorSchemes.Parse(hex));
            Border Bar(double width, double height, string hex) =>
                new() { Width = width, Height = height, CornerRadius = new CornerRadius(2), Background = B(hex), HorizontalAlignment = HorizontalAlignment.Left };

            var picture = new Grid { Width = 160, Height = 100, CornerRadius = new CornerRadius(4), BorderBrush = B(p.Border), BorderThickness = new Thickness(1) };
            picture.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            picture.RowDefinitions.Add(new RowDefinition());
            picture.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            picture.ColumnDefinitions.Add(new ColumnDefinition());
            var band = new Border { Background = B(p.TabStrip), Child = new StackPanel { Orientation = Orientation.Horizontal, Padding = new Thickness(8, 0, 0, 0), Spacing = 8 } };
            var tabs = (StackPanel)band.Child;
            tabs.VerticalAlignment = VerticalAlignment.Center;
            tabs.Children.Add(Bar(26, 4, p.TabText));
            tabs.Children.Add(Bar(20, 4, p.TabIcon));
            Grid.SetColumnSpan(band, 2);
            var side = new StackPanel { Background = B(p.Background), Padding = new Thickness(7, 8, 7, 0), Spacing = 6 };
            side.Children.Add(Bar(30, 6, p.Primary));
            side.Children.Add(Bar(26, 3, p.Text2));
            side.Children.Add(Bar(22, 3, p.Text2));
            Grid.SetRow(side, 1);
            var page = new StackPanel { Background = B(p.Background), Padding = new Thickness(10, 10, 8, 0), Spacing = 6 };
            page.Children.Add(Bar(54, 6, p.Text));
            page.Children.Add(Bar(80, 4, p.Text2));
            page.Children.Add(Bar(66, 4, p.Text2));
            page.Children.Add(Bar(30, 10, p.Primary));
            Grid.SetRow(page, 1);
            Grid.SetColumn(page, 1);
            var divider = new Border { Width = 1, Background = B(p.Border), HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetRow(divider, 1);
            Grid.SetColumn(divider, 1);
            picture.Children.Add(band);
            picture.Children.Add(side);
            picture.Children.Add(page);
            picture.Children.Add(divider);

            // The check mark of the chosen card (shown by UpdateSchemeCards).
            var check = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0), Background = Resource("CaretPrimaryBrush"), Visibility = Visibility.Collapsed,
                Child = new FontIcon { Glyph = "", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)) },
            };
            AutomationProperties.SetAccessibilityView(check, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
            var thumbnail = new Grid();
            thumbnail.Children.Add(picture);
            thumbnail.Children.Add(check);

            var stack = new StackPanel();
            stack.Children.Add(thumbnail);
            stack.Children.Add(new TextBlock { Text = Locale.GetString(scheme.NameKey), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 2) });
            var card = new Border
            {
                Tag = scheme.Id, Child = stack, Padding = new Thickness(4), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                BorderBrush = Resource("CardStrokeColorDefaultBrush"), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            };
            card.PointerEntered += (_, _) => { if ((string)card.Tag != settings.ColorScheme) card.BorderBrush = Resource("CaretPrimaryBrush"); };
            card.PointerExited += (_, _) => { if ((string)card.Tag != settings.ColorScheme) card.BorderBrush = Resource("CardStrokeColorDefaultBrush"); };
            AutomationProperties.SetName(card, Locale.GetString(scheme.NameKey));
            return card;
        }

        // 1 px resting border, the accent colour under the pointer (PointerEntered above), 2 px accent and a check mark when chosen. The padding
        // takes the extra pixel so the cards do not move.
        private void UpdateSchemeCards()
        {
            foreach (var card in ColorSchemeGridView.Items.OfType<Border>())
            {
                var chosen = (string)card.Tag == settings.ColorScheme;
                card.BorderThickness = new Thickness(chosen ? 2 : 1);
                card.Padding = new Thickness(chosen ? 3 : 4);
                card.BorderBrush = Resource(chosen ? "CaretPrimaryBrush" : "CardStrokeColorDefaultBrush");
                var thumbnail = (Grid)((StackPanel)card.Child).Children[0];
                thumbnail.Children[1].Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // --- The live preview ---

        // A picture of the window in the scheme that is chosen, with the section colours that are kept (and pass the guard, as in the window).
        private void RefreshAppearancePreview()
        {
            if (AppearancePreviewHost == null) return;
            var dark = IsDark;
            var c = AppearancePreview.Resolve(SchemeInput(dark), StoredSectionColors(dark), dark);
            Brush B(string hex) => new SolidColorBrush(ColorSchemes.Parse(hex));
            Border Bar(double width, double height, string hex, Thickness? margin = null) =>
                new() { Width = width, Height = height, CornerRadius = new CornerRadius(2), Background = B(hex), HorizontalAlignment = HorizontalAlignment.Left, Margin = margin ?? new Thickness(0) };

            var window = new Grid { Width = 440, Height = 250 };
            window.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            window.RowDefinitions.Add(new RowDefinition());
            window.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            window.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(122) });
            window.ColumnDefinitions.Add(new ColumnDefinition());

            // title bar and tabs: the open tab is the page's colour
            var band = new Grid { Background = B(c.BandBackground), CornerRadius = new CornerRadius(7, 7, 0, 0) };
            Grid.SetColumnSpan(band, 2);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Padding = new Thickness(10, 7, 0, 0) };
            var open = new Border { Width = 96, Height = 23, CornerRadius = new CornerRadius(6, 6, 0, 0), Background = B(c.PageBackground), Child = Bar(54, 4, c.PageText2) };
            ((Border)open.Child).VerticalAlignment = VerticalAlignment.Center;
            ((Border)open.Child).Margin = new Thickness(12, 0, 0, 0);
            tabs.Children.Add(open);
            var other = Bar(52, 4, c.BandText);
            other.VerticalAlignment = VerticalAlignment.Center;
            tabs.Children.Add(other);
            band.Children.Add(tabs);

            // sidebar: a New note button, then the items, the first one chosen
            var side = new StackPanel { Background = B(c.SideBackground), Padding = new Thickness(10, 12, 10, 0), Spacing = 8 };
            Grid.SetRow(side, 1);
            side.Children.Add(new Border { Height = 20, CornerRadius = new CornerRadius(4), Background = B(c.Accent), Child = Bar(46, 4, "#FFFFFF", new Thickness(10, 0, 0, 0)) });
            ((Border)((Border)side.Children[0]).Child).VerticalAlignment = VerticalAlignment.Center;
            var widths = new[] { 62, 50, 70, 44, 58 };
            for (var i = 0; i < widths.Length; i++)
            {
                var row = new Border { Height = 18, CornerRadius = new CornerRadius(4), Background = i == 0 ? B(c.SideSelected) : null };
                var line = Bar(widths[i], 4, i == 0 ? c.SideText : c.SideText2, new Thickness(8, 0, 0, 0));
                line.VerticalAlignment = VerticalAlignment.Center;
                row.Child = line;
                side.Children.Add(row);
            }

            // page: a heading, some lines, a button and a link
            var page = new StackPanel { Background = B(c.PageBackground), Padding = new Thickness(20, 18, 16, 0), Spacing = 9 };
            Grid.SetRow(page, 1);
            Grid.SetColumn(page, 1);
            page.Children.Add(Bar(150, 9, c.PageText));
            foreach (var width in new[] { 250, 230, 258, 170 }) page.Children.Add(Bar(width, 4, c.PageText2));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(0, 8, 0, 0) };
            actions.Children.Add(new Border { Width = 64, Height = 18, CornerRadius = new CornerRadius(4), Background = B(c.Accent) });
            var link = Bar(60, 4, c.Accent);
            link.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(link);
            page.Children.Add(actions);

            // status bar
            var status = new Grid { Background = B(c.StatusBackground), CornerRadius = new CornerRadius(0, 0, 7, 7) };
            Grid.SetRow(status, 2);
            Grid.SetColumnSpan(status, 2);
            var statusLine = Bar(84, 3, c.StatusText, new Thickness(10, 0, 0, 0));
            statusLine.VerticalAlignment = VerticalAlignment.Center;
            status.Children.Add(statusLine);

            window.Children.Add(band);
            window.Children.Add(side);
            window.Children.Add(page);
            window.Children.Add(status);
            var frame = new Border { Child = window, CornerRadius = new CornerRadius(8), BorderBrush = B(c.Border), BorderThickness = new Thickness(1), Background = B(c.PageBackground) };
            AutomationProperties.SetName(frame, Locale.GetString("AppearancePreview"));
            AppearancePreviewHost.Child = frame;
        }

        // --- Custom and Reset to scheme ---

        private Border SectionCustomOf(string section) => section switch
        {
            "band" => SectionBandCustom, "side" => SectionSideCustom, "page" => SectionPageCustom, _ => SectionStatusCustom,
        };

        private Button SectionResetOf(string section) => section switch
        {
            "band" => SectionBandReset, "side" => SectionSideReset, "page" => SectionPageReset, _ => SectionStatusReset,
        };

        // A row says "Custom" when it keeps a colour that is not the scheme's own; Reset to scheme is there while any colour is kept. The
        // expander says so too, so a colour kept under a collapsed expander is still seen.
        private void UpdateSectionCustomState()
        {
            var dark = IsDark;
            var stored = StoredSectionColors(dark);
            var scheme = SchemeInput(dark);
            var kept = SectionColorChoice.Parse(stored, dark);
            foreach (var section in SectionColorChoice.Sections)
            {
                SectionCustomOf(section).Visibility = AppearancePreview.IsCustom(stored, dark, section, scheme) ? Visibility.Visible : Visibility.Collapsed;
                SectionResetOf(section).IsEnabled = kept.ContainsKey(section);
            }
            SectionColorsCustomBadge.Visibility = AppearancePreview.AnyCustom(stored, dark, scheme) ? Visibility.Visible : Visibility.Collapsed;
            SectionColorsResetAll.IsEnabled = kept.Count > 0;
        }

        // One area back to the scheme's colour: the same as choosing "From the colour scheme" in its list.
        private void SectionReset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string section) ChangeSectionColor(section, "");
        }

        private void SectionResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (sectionListsDark != IsDark) { LoadSectionColorSettings(); return; }
            StoreSectionColors(sectionListsDark, "");
            LoadSectionColorSettings();
        }
    }
}
