using Windows.UI.Xaml.Markup;

namespace Typedown.Universal.Utilities
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class StringResource : MarkupExtension
    {
        public string Key { get; set; }

        public StringResource()
        {

        }

        public StringResource(string key)
        {
            Key = key;
        }

        protected override object ProvideValue()
        {
            return Localize.GetString(Key);
        }
    }
}
