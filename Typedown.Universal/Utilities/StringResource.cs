using Windows.UI.Xaml.Markup;

namespace Typedown.Universal.Utilities
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class StringResource : MarkupExtension
    {
        public string Key { get; set; }

        public string Source { get; set; }

        protected override object ProvideValue()
        {
            return Localize.GetString(Key, Source);
        }
    }
}
