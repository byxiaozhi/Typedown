using Microsoft.UI.Xaml.Markup;

namespace Typedown.WinUI.Utilities
{
    // New since the fork: {u:Loc Key=...} in XAML looks the text up in the loaded language's .resw files
    // (Utilities/Locale.cs), the same store the code uses via Locale.GetString. Resolved once, when the
    // XAML loads, so changing the language takes effect on the next start. Falls back to the key itself
    // so a missing string is visible rather than blank.
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        protected override object ProvideValue() => Locale.GetString(Key) ?? Key;
    }
}
