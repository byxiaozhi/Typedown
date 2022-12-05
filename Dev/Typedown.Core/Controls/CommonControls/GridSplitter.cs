using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Typedown.Core.Controls
{
    public class GridSplitter : UserControl
    {
        public static DependencyProperty ColumnWidthProperty = DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(GridSplitter), null);
        public double ColumnWidth { get => (double)GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

        public static DependencyProperty ColumnExpectWidthProperty = DependencyProperty.Register(nameof(ColumnExpectWidth), typeof(double), typeof(GridSplitter), new(0d, OnPropertyChanged));
        public double ColumnExpectWidth { get => (double)GetValue(ColumnExpectWidthProperty); set => SetValue(ColumnExpectWidthProperty, value); }

        public static DependencyProperty ColumnMinWidthProperty = DependencyProperty.Register(nameof(ColumnMinWidth), typeof(double), typeof(GridSplitter), new(0d, OnPropertyChanged));
        public double ColumnMinWidth { get => (double)GetValue(ColumnMinWidthProperty); set => SetValue(ColumnMinWidthProperty, value); }

        public static DependencyProperty ColumnMaxWidthProperty = DependencyProperty.Register(nameof(ColumnMaxWidth), typeof(double), typeof(GridSplitter), new(double.PositiveInfinity, OnPropertyChanged));
        public double ColumnMaxWidth { get => (double)GetValue(ColumnMaxWidthProperty); set => SetValue(ColumnMaxWidthProperty, value); }

        public static DependencyProperty DeltaScaleProperty = DependencyProperty.Register(nameof(DeltaScale), typeof(double), typeof(GridSplitter), new(1d));
        public double DeltaScale { get => (double)GetValue(DeltaScaleProperty); set => SetValue(DeltaScaleProperty, value); }

        private readonly Border border = new();

        private double columnWidth;

        private bool manipulating;

        private bool entered;

        public GridSplitter()
        {
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.Width = 9;
            Margin = new Thickness(-4, 0, -4, 0);
            Content = border;
            ManipulationMode = ManipulationModes.TranslateX;
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            entered = true;
            Window.Current.CoreWindow.PointerCursor = new(CoreCursorType.SizeWestEast, 1);

        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            entered = false;
            if (!manipulating)
                Window.Current.CoreWindow.PointerCursor = new(CoreCursorType.Arrow, 1);
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            base.OnManipulationStarted(e);
            columnWidth = ColumnWidth;
            manipulating = true;
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            base.OnManipulationDelta(e);
            var scale = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            columnWidth += DeltaScale * e.Delta.Translation.X * scale;
            ColumnExpectWidth = Math.Min(Math.Max(ColumnMinWidth, columnWidth), ColumnMaxWidth);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            base.OnManipulationCompleted(e);
            manipulating = false;
            if (!entered)
                Window.Current.CoreWindow.PointerCursor = new(CoreCursorType.Arrow, 1);
        }

        public static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as GridSplitter;
            var limitedWidth = Math.Min(Math.Max(target.ColumnMinWidth, target.ColumnExpectWidth), target.ColumnMaxWidth);
            if (limitedWidth != target.ColumnWidth)
                target.ColumnWidth = limitedWidth;
        }
    }
}
