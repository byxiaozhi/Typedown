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
        // --- Section colours (what each choice means: Utilities/SectionColorChoice.cs) ---

        private bool IsDark => ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;

        // The stored choices of one theme, as SectionColorChoice reads them: a colour of that theme's own list only.
        private Dictionary<string, string> SectionColors(bool dark) =>
            SectionColorChoice.Parse(dark ? settings.SectionColorsDark : settings.SectionColorsLight, dark);

        private static bool IsHexColor(string value) => SectionColorChoice.IsHexColor(value);

        // The contrast guard (SectionColorChoice.Check), against the scheme's text colour.
        private static (bool Ok, Color Text, Color Text2, double Ratio) Guard(string hex, string section, bool dark)
        {
            var verdict = SectionColorChoice.Check(hex, section, ColorSchemes.Current(dark).Text);
            return (verdict.Ok, ColorSchemes.Parse(verdict.Text), ColorSchemes.Parse(verdict.Text2), verdict.Ratio);
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

        // The theme the lists were built for: the swatches differ between light and dark, so a list built before
        // the theme changed must not be read as the new theme's.
        private bool sectionListsDark;

        private ComboBox SectionComboOf(string section) => section switch
        {
            "band" => SectionBandComboBox, "side" => SectionSideComboBox, "page" => SectionPageComboBox, _ => SectionStatusComboBox,
        };

        private TextBlock SectionChipOf(string section) => section switch
        {
            "band" => SectionBandContrast, "side" => SectionSideContrast, "page" => SectionPageContrast, _ => SectionStatusContrast,
        };

        private void LoadSectionColorSettings()
        {
            fillingSections = true;
            var dark = sectionListsDark = IsDark;
            var schemeText = ColorSchemes.Current(dark).Text;
            var stored = dark ? settings.SectionColorsDark : settings.SectionColorsLight;
            foreach (var section in SectionColorChoice.Sections)
            {
                var combo = SectionComboOf(section);
                var rows = SectionColorChoice.Rows(section, dark, schemeText);
                combo.Items.Clear();
                foreach (var row in rows)
                {
                    if (row.NameKey == null)
                    {
                        // "From the colour scheme" shows the colour the scheme gives this area, as the others show theirs.
                        var own = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        own.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(ColorSchemes.Parse(AppearancePreview.SchemeColor(SchemeInput(dark), section))),
                            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center });
                        own.Children.Add(new TextBlock { Text = Locale.GetString("SwatchDefault"), VerticalAlignment = VerticalAlignment.Center });
                        var defaultItem = new ComboBoxItem { Content = own, Tag = "" };
                        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(defaultItem, Locale.GetString("SwatchDefault"));
                        combo.Items.Add(defaultItem);
                        continue;
                    }
                    var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    line.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(ColorSchemes.Parse(row.Hex)),
                        BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center });
                    line.Children.Add(new TextBlock { Text = Locale.GetString(row.NameKey), VerticalAlignment = VerticalAlignment.Center });
                    var item = new ComboBoxItem { Content = line, Tag = row.Hex, IsEnabled = row.Enabled };
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, Locale.GetString(row.NameKey));
                    if (!row.Enabled) ToolTipService.SetToolTip(item, $"{Locale.GetString("TooLittleContrast")} ({row.Ratio:0.0} : 1)");
                    combo.Items.Add(item);
                }
                // The stored choice shows as chosen even when it can't be used now (another colour scheme), so the
                // list never says "from the colour scheme" while a colour is kept; the contrast says why it isn't applied.
                combo.SelectedIndex = SectionColorChoice.SelectedRow(rows, stored, dark, section);
                ShowSectionContrast(section, SectionChipOf(section), (combo.SelectedItem as ComboBoxItem)?.Tag as string, dark);
            }
            fillingSections = false;
            UpdateSectionCustomState();
            RefreshAppearancePreview();
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
            // The lists are of another theme than the window's now (it changed while Settings was open and the
            // lists weren't rebuilt): a choice made from them would be stored for the wrong theme. Show the right ones.
            if (sectionListsDark != IsDark) { LoadSectionColorSettings(); return; }
            var combo = (ComboBox)sender;
            var section = (string)combo.Tag;
            var hex = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            ChangeSectionColor(section, hex, reload: false);
            ShowSectionContrast(section, SectionChipOf(section), hex, sectionListsDark);
            UpdateSectionCustomState();
            RefreshAppearancePreview();
        }

        // One area's colour ("" is the scheme's own), kept for the theme the lists are of and applied in every window. `reload`: the lists are
        // drawn again (a Reset to scheme, which did not come from a list).
        private void ChangeSectionColor(string section, string hex, bool reload = true)
        {
            if (sectionListsDark != IsDark) { LoadSectionColorSettings(); return; }
            var dark = sectionListsDark;
            StoreSectionColors(dark, SectionColorChoice.With(StoredSectionColors(dark), dark, section, hex));
            if (reload) LoadSectionColorSettings();
        }

        // The stored list of one theme, set once and copied to every window (each saves it) and applied there. Reset all uses this too, so the
        // settings are written once for it, not once per area.
        private void StoreSectionColors(bool dark, string value)
        {
            if (dark) settings.SectionColorsDark = value;
            else settings.SectionColorsLight = value;
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
