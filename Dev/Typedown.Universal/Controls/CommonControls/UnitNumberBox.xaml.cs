using System;
using System.Collections.Generic;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class UnitNumberBox : muxc.NumberBox
    {
        public static DependencyProperty UnitsProperty { get; } = DependencyProperty.Register(nameof(Units), typeof(IReadOnlyList<NumberUnit>), typeof(UnitNumberBox), new(null, (d, e) => (d as UnitNumberBox).OnDependencyPropertyChanged(e)));

        public IReadOnlyList<NumberUnit> Units { get => (IReadOnlyList<NumberUnit>)GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }

        public static DependencyProperty SelectedUnitProperty { get; } = DependencyProperty.Register(nameof(SelectedUnit), typeof(NumberUnit), typeof(UnitNumberBox), new(null, (d, e) => (d as UnitNumberBox).OnDependencyPropertyChanged(e)));

        public NumberUnit SelectedUnit { get => (NumberUnit)GetValue(SelectedUnitProperty); set => SetValue(SelectedUnitProperty, value); }

        public static DependencyProperty DimNumberValueProperty { get; } = DependencyProperty.Register(nameof(DimNumberValue), typeof(DimNumber), typeof(UnitNumberBox), new(null, (d, e) => (d as UnitNumberBox).OnDependencyPropertyChanged(e)));

        public DimNumber DimNumberValue { get => (DimNumber)GetValue(DimNumberValueProperty); set => SetValue(DimNumberValueProperty, value); }

        public ComboBox UnitComboBox { get; set; }

        public event EventHandler<NumberUnit> SelectedUnitChanged;

        public UnitNumberBox()
        {
            this.InitializeComponent();
        }

        private void OnUnitComboBoxLoaded(object sender, RoutedEventArgs e)
        {
            UnitComboBox = sender as ComboBox;
            UnitComboBox.SetBinding(ComboBox.SelectedItemProperty, new Binding() { Source = this, Path = new(nameof(SelectedUnit)), Mode = BindingMode.TwoWay });
            UnitComboBox.SetBinding(ComboBox.ItemsSourceProperty, new Binding() { Source = this, Path = new(nameof(Units)) });
            (UnitComboBox.GetTemplateChild("LayoutRoot") as Grid).ColumnDefinitions[1].MaxWidth = 0;
        }

        private void OnUnitComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedUnitChanged?.Invoke(this, SelectedUnit);
        }

        private void OnValueChanged(muxc.NumberBox sender, muxc.NumberBoxValueChangedEventArgs args)
        {
            if (DimNumberValue.Value != Value)
                DimNumberValue = new(SelectedUnit, Value);
        }

        private void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs e)
        {

            if (e.Property == SelectedUnitProperty)
            {
                if (DimNumberValue.Unit != SelectedUnit)
                    DimNumberValue = new(SelectedUnit, Value);
            }
            if (e.Property == DimNumberValueProperty)
            {
                Value = DimNumberValue.Value;
                SelectedUnit = DimNumberValue.Unit;
            }
        }
    }
}
