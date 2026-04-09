using System;
using System.Collections.ObjectModel;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Typedown.Core.Controls.EditorControls.ContextMenuItems
{
    public sealed partial class FormatItem : MenuItem
    {
        public event EventHandler ItemClick;

        public FormatItem()
        {
            InitializeComponent();
            AddHandler(PointerReleasedEvent, new PointerEventHandler(OnMenuItemPointerReleased), true);
        }

        private void OnMenuItemPointerReleased(object sender, PointerEventArgs e)
        {
            ItemClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
