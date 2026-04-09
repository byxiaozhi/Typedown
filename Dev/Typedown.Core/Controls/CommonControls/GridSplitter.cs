using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Typedown.Core.Controls
{
    public class GridSplitter : UserControl
    {
        public static readonly StyledProperty<double> ColumnWidthProperty = AvaloniaProperty.Register<GridSplitter, double>(nameof(ColumnWidth), 0);
        public double ColumnWidth { get => GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

        public static readonly StyledProperty<double> ColumnExpectWidthProperty = AvaloniaProperty.Register<GridSplitter, double>(nameof(ColumnExpectWidth), 0d);
        public double ColumnExpectWidth { get => GetValue(ColumnExpectWidthProperty); set => SetValue(ColumnExpectWidthProperty, value); }

        public static readonly StyledProperty<double> ColumnMinWidthProperty = AvaloniaProperty.Register<GridSplitter, double>(nameof(ColumnMinWidth), 0d);
        public double ColumnMinWidth { get => GetValue(ColumnMinWidthProperty); set => SetValue(ColumnMinWidthProperty, value); }

        public static readonly StyledProperty<double> ColumnMaxWidthProperty = AvaloniaProperty.Register<GridSplitter, double>(nameof(ColumnMaxWidth), double.PositiveInfinity);
        public double ColumnMaxWidth { get => GetValue(ColumnMaxWidthProperty); set => SetValue(ColumnMaxWidthProperty, value); }

        public static readonly StyledProperty<double> DeltaScaleProperty = AvaloniaProperty.Register<GridSplitter, double>(nameof(DeltaScale), 1d);
        public double DeltaScale { get => GetValue(DeltaScaleProperty); set => SetValue(DeltaScaleProperty, value); }

        private readonly Border border = new();

        private double columnWidth;
        private bool manipulating;
        private Point _startPoint;

        public GridSplitter()
        {
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.Width = 9;
            Margin = new Thickness(-4, 0, -4, 0);
            Content = border;
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            columnWidth = ColumnWidth;
            manipulating = true;
            _startPoint = e.GetPosition(this.Parent as Visual);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!manipulating) return;

            var currentPoint = e.GetPosition(this.Parent as Visual);
            var deltaX = currentPoint.X - _startPoint.X;

            columnWidth += DeltaScale * deltaX; // scale isn't strictly necessary with Avalonia DIPs unless handling DPI explicitly
            ColumnExpectWidth = Math.Min(Math.Max(ColumnMinWidth, columnWidth), ColumnMaxWidth);
            
            _startPoint = currentPoint;
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (manipulating)
            {
                manipulating = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ColumnExpectWidthProperty || change.Property == ColumnMinWidthProperty || change.Property == ColumnMaxWidthProperty)
            {
                var limitedWidth = Math.Min(Math.Max(ColumnMinWidth, ColumnExpectWidth), ColumnMaxWidth);
                if (limitedWidth != ColumnWidth)
                    ColumnWidth = limitedWidth;
            }
        }
    }
}

