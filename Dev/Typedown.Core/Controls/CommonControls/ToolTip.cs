using System;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls
{
    public class ToolTip
    {
        public static readonly AttachedProperty<string> TextResourceProperty = AvaloniaProperty.RegisterAttached<ToolTip, Control, string>("TextResource", null);
        public static string GetTextResource(Control target) => target.GetValue(TextResourceProperty);
        public static void SetTextResource(Control target, string value) => target.SetValue(TextResourceProperty, value);

        static ToolTip()
        {
            TextResourceProperty.Changed.Subscribe(e =>
            {
                if (e.Sender is Control target)
                {
                    if (e.NewValue.HasValue && e.NewValue.Value is string resource && !string.IsNullOrEmpty(resource))
                        Avalonia.Controls.ToolTip.SetTip(target, Locale.GetString(resource));
                    else
                        Avalonia.Controls.ToolTip.SetTip(target, null);
                }
            });
        }
    }
}

