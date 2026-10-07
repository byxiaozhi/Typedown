using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;
using Windows.UI;

namespace Typedown.WinUI
{
    // Interface review, phase 3: colours for one area at a time (the title bar and tabs, the sidebar,
    // the editor page, the status bar) over the colour scheme, and a colour per tab.
    public sealed partial class MainWindow
    {
        // --- Section colours ---

        public sealed record Swatch(string NameKey, string Hex);

        private static readonly Swatch[] LightSwatches =
        {
            new("SwatchWarmSand", "#EAD9C4"), new("SchemeSage", "#DCE7DA"), new("SwatchMist", "#DCE6EF"), new("SwatchStone", "#E4E2DE"),
            new("SwatchLavender", "#E6E1F0"), new("SchemeCopper", "#A5522A"), new("SwatchEspresso", "#3B2A1E"),
        };

        private static readonly Swatch[] DarkSwatches =
        {
            new("SwatchUmber", "#221A13"), new("SwatchMoss", "#15201A"), new("SwatchNight", "#111C28"), new("SwatchCharcoal", "#1E1E1E"),
            new("SwatchPlum", "#1C1626"), new("SchemeCopper", "#8F4A22"), new("SchemePaper", "#F3F3F3"),
        };

        private static readonly string[] Sections = { "band", "side", "page", "status" };

        private bool IsDark => ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;

        // "band=#EAD9C4;page=#DCE6EF", one setting per theme: a colour chosen in light isn't meant for dark.
        // Anything else in the setting (edited by hand, an older format) is ignored rather than trusted.
        private Dictionary<string, string> SectionColors(bool dark) =>
            ((dark ? settings.SectionColorsDark : settings.SectionColorsLight) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=')).Where(p => p.Length == 2 && Sections.Contains(p[0]) && IsHexColor(p[1]))
                .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);

        private static bool IsHexColor(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^#[0-9A-Fa-f]{6}$");

        private void SaveSectionColors(bool dark, Dictionary<string, string> colors)
        {
            var value = string.Join(";", colors.Select(c => $"{c.Key}={c.Value}"));
            if (dark) settings.SectionColorsDark = value;
            else settings.SectionColorsLight = value;
        }

        // The contrast guard. Text on the area is the scheme's text or its opposite, whichever reaches
        // 4.5 : 1 (WCAG 2.2 AA) first; the page keeps the scheme's text, which the editor draws. A
        // colour on which neither reaches 4.5 isn't offered. Secondary text is the text colour softened
        // while it stays at 4.5 or more.
        private static (bool Ok, Color Text, Color Text2, double Ratio) Guard(string hex, string section, bool dark)
        {
            var background = ColorSchemes.Parse(hex);
            var palette = ColorSchemes.Current(dark);
            var text = ColorSchemes.Parse(palette.Text);
            var inverse = ColorSchemes.Contrast(text, Microsoft.UI.Colors.White) > ColorSchemes.Contrast(text, Microsoft.UI.Colors.Black)
                ? Microsoft.UI.Colors.White : ColorSchemes.Parse("#141414");
            var candidates = section == "page" ? new[] { text } : new[] { text, inverse };
            foreach (var candidate in candidates)
            {
                var ratio = ColorSchemes.Contrast(candidate, background);
                if (ratio < 4.5) continue;
                var soft = Mix(candidate, background, 0.28);
                return (true, candidate, ColorSchemes.Contrast(soft, background) >= 4.5 ? soft : candidate, ratio);
            }
            return (false, text, text, candidates.Max(c => ColorSchemes.Contrast(c, background)));
        }

        private static Color Mix(Color a, Color b, double t) => Color.FromArgb(0xFF,
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

        private static readonly Dictionary<string, string[]> SectionKeys = new()
        {
            ["band"] = new[] { "CaretTabStripBrush", "TabViewItemHeaderForeground", "TabViewItemIconForeground", "TabViewButtonForeground",
                "TabViewItemSeparator", "TabViewItemHeaderBackgroundPointerOver", "TabViewItemHeaderBackgroundPressed", "TabViewButtonBackgroundPointerOver",
                "CaretIconBrush", "TextFillColorPrimaryBrush" },
            ["side"] = new[] { "CaretSidebarBrush", "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "CaretIconBrush", "CaretTextPrimaryBrush",
                "ListViewItemForeground", "ListViewItemForegroundPointerOver", "ListViewItemForegroundSelected", "ListViewItemBackgroundPointerOver",
                "ListViewItemBackgroundSelected", "ListViewItemBackgroundSelectedPointerOver", "TreeViewItemForeground", "TreeViewItemForegroundPointerOver" },
            ["status"] = new[] { "CaretStatusBarBrush", "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "ControlFillColorSecondaryBrush" },
        };

        // Each area takes its colours as resources of its own, which its content (and the stock controls'
        // templates) look up before the app's; re-read by flipping the area's theme.
        private void ApplySectionColors()
        {
            var dark = IsDark;
            var colors = SectionColors(dark);
            foreach (var (section, element) in new (string, FrameworkElement)[] { ("band", AppTitleBar), ("side", TocPane), ("status", StatusBar) })
            {
                foreach (var key in SectionKeys[section]) element.Resources.Remove(key);
                if (colors.TryGetValue(section, out var hex) && Guard(hex, section, dark) is { Ok: true } guard)
                {
                    var bg = ColorSchemes.Parse(hex);
                    SolidColorBrush B(Color c) => new(c);
                    var r = element.Resources;
                    r[section switch { "band" => "CaretTabStripBrush", "side" => "CaretSidebarBrush", _ => "CaretStatusBarBrush" }] = B(bg);
                    r["TextFillColorPrimaryBrush"] = B(guard.Text);
                    if (section != "band") r["TextFillColorSecondaryBrush"] = B(guard.Text2);
                    if (section is "band" or "side") r["CaretIconBrush"] = B(guard.Text2);
                    if (section == "band")
                    {
                        r["TabViewItemHeaderForeground"] = r["TabViewItemIconForeground"] = r["TabViewButtonForeground"] = B(guard.Text2);
                        r["TabViewItemSeparator"] = B(Mix(guard.Text, bg, 0.6));
                        r["TabViewItemHeaderBackgroundPointerOver"] = r["TabViewButtonBackgroundPointerOver"] = B(Mix(bg, guard.Text, 0.07));
                        r["TabViewItemHeaderBackgroundPressed"] = B(Mix(bg, guard.Text, 0.11));
                    }
                    if (section == "side")
                    {
                        r["CaretTextPrimaryBrush"] = r["ListViewItemForeground"] = r["ListViewItemForegroundPointerOver"] = r["ListViewItemForegroundSelected"]
                            = r["TreeViewItemForeground"] = r["TreeViewItemForegroundPointerOver"] = B(guard.Text);
                        r["ListViewItemBackgroundPointerOver"] = B(Mix(bg, guard.Text, 0.07));
                        r["ListViewItemBackgroundSelected"] = r["ListViewItemBackgroundSelectedPointerOver"] = B(Mix(bg, guard.Text, 0.12));
                    }
                    if (section == "status") r["ControlFillColorSecondaryBrush"] = B(Mix(bg, guard.Text, 0.08));
                }
                ColorSchemes.Refresh(element);
            }
            ApplyEditorBackground();
            PushThemeToEditor();
        }

        // The editor page's colour: the section colour when there is one that passes, else the scheme's.
        private Color PageBackground(bool dark) =>
            SectionColors(dark).TryGetValue("page", out var hex) && Guard(hex, "page", dark).Ok
                ? ColorSchemes.Parse(hex) : ColorSchemes.Parse(ColorSchemes.Current(dark).Background);

        // Settings > Appearance > Section colours: one list per area, the colours that would be hard to
        // read disabled with the reason, and the contrast of the one chosen.
        private bool fillingSections;

        private void LoadSectionColorSettings()
        {
            fillingSections = true;
            var dark = IsDark;
            var colors = SectionColors(dark);
            foreach (var (section, combo, chip) in new[] { ("band", SectionBandComboBox, SectionBandContrast), ("side", SectionSideComboBox, SectionSideContrast),
                ("page", SectionPageComboBox, SectionPageContrast), ("status", SectionStatusComboBox, SectionStatusContrast) })
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = Locale.GetString("SwatchDefault"), Tag = "" });
                foreach (var swatch in dark ? DarkSwatches : LightSwatches)
                {
                    var guard = Guard(swatch.Hex, section, dark);
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    row.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(ColorSchemes.Parse(swatch.Hex)),
                        BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center });
                    row.Children.Add(new TextBlock { Text = Locale.GetString(swatch.NameKey), VerticalAlignment = VerticalAlignment.Center });
                    var item = new ComboBoxItem { Content = row, Tag = swatch.Hex, IsEnabled = guard.Ok };
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, Locale.GetString(swatch.NameKey));
                    if (!guard.Ok) ToolTipService.SetToolTip(item, $"{Locale.GetString("TooLittleContrast")} ({guard.Ratio:0.0} : 1)");
                    combo.Items.Add(item);
                }
                colors.TryGetValue(section, out var current);
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == (current ?? "") && i.IsEnabled) ?? combo.Items[0];
                ShowSectionContrast(section, chip, current, dark);
            }
            fillingSections = false;
        }

        private void ShowSectionContrast(string section, TextBlock chip, string hex, bool dark)
        {
            if (string.IsNullOrEmpty(hex)) { chip.Text = ""; return; }
            var guard = Guard(hex, section, dark);
            chip.Text = guard.Ok ? $"{guard.Ratio:0.0} : 1" : Locale.GetString("TooLittleContrast");
        }

        private void SectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fillingSections || suppressSettingsEvents) return;
            var combo = (ComboBox)sender;
            var section = (string)combo.Tag;
            var hex = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var dark = IsDark;
            var colors = SectionColors(dark);
            if (hex.Length == 0) colors.Remove(section);
            else colors[section] = hex;
            SaveSectionColors(dark, colors);
            ShowSectionContrast(section, section switch { "band" => SectionBandContrast, "side" => SectionSideContrast, "page" => SectionPageContrast, _ => SectionStatusContrast }, hex, dark);
            foreach (var window in openWindows.ToList())
            {
                window.settings.SectionColorsLight = settings.SectionColorsLight;
                window.settings.SectionColorsDark = settings.SectionColorsDark;
                window.ApplySectionColors();
            }
        }

        // --- Tab colours: right-click a tab > Tab color, kept with the file's path ---

        private static readonly (string NameKey, string Hex)[] TabColors =
        {
            ("ColorRed", "#C42B1C"), ("ColorOrange", "#CA5010"), ("ColorYellow", "#C19C00"), ("ColorGreen", "#107C10"),
            ("ColorTeal", "#038387"), ("ColorBlue", "#0063B1"), ("ColorPurple", "#8764B8"), ("ColorPink", "#E3008C"),
        };

        private Dictionary<string, string> TabColorMap() =>
            (settings.TabColors ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('\t'))
                .Where(p => p.Length == 2 && IsHexColor(p[1])).GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First()[1], StringComparer.OrdinalIgnoreCase);

        private string TabColorOf(DocumentTab doc) =>
            !string.IsNullOrEmpty(doc.Path) && TabColorMap().TryGetValue(doc.Path, out var hex) ? hex : null;

        private void SetTabColor(DocumentTab doc, string hex)
        {
            if (string.IsNullOrEmpty(doc.Path)) return;
            var map = TabColorMap();
            if (hex == null) map.Remove(doc.Path);
            else map[doc.Path] = hex;
            settings.TabColors = string.Join("\n", map.Select(m => $"{m.Key}\t{m.Value}"));
            foreach (var window in openWindows.ToList())
            {
                window.settings.TabColors = settings.TabColors;
                foreach (var d in window.documents) window.UpdateTabHeader(d);
            }
        }

        private MenuFlyoutSubItem BuildTabColorMenu(DocumentTab doc)
        {
            var menu = new MenuFlyoutSubItem { Text = Locale.GetString("TabColorMenu") };
            RadioMenuFlyoutItem Item(string nameKey, string hex)
            {
                var item = new RadioMenuFlyoutItem { Text = Locale.GetString(nameKey), GroupName = "TabColor", Tag = hex };
                if (hex != null) item.Icon = new FontIcon { Glyph = "●", FontFamily = new FontFamily("Segoe UI"), Foreground = new SolidColorBrush(ColorSchemes.Parse(hex)) };
                item.Click += (s, e) => SetTabColor(doc, hex);
                menu.Items.Add(item);
                return item;
            }
            Item("ColorNone", null);
            foreach (var (nameKey, hex) in TabColors) Item(nameKey, hex);
            return menu;
        }
    }
}
