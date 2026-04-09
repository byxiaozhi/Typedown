using Avalonia.Markup.Xaml;
using System;

namespace Typedown.Core.Utilities
{
    public class LocaleStringExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocaleStringExtension() { }

        public LocaleStringExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Locale.GetString(Key ?? "");
        }
    }
}
