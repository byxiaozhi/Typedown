using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls;
namespace Typedown.Core.Controls
{
    public sealed partial class UnitNumberBox : NumericUpDown
    {
        public static readonly StyledProperty<IReadOnlyList<NumberUnit>> UnitsProperty = AvaloniaProperty.Register<UnitNumberBox, IReadOnlyList<NumberUnit>>(nameof(Units), null);
        public IReadOnlyList<NumberUnit> Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }

        public static readonly StyledProperty<NumberUnit> SelectedUnitProperty = AvaloniaProperty.Register<UnitNumberBox, NumberUnit>(nameof(SelectedUnit), default(NumberUnit));
        public NumberUnit SelectedUnit { get => GetValue(SelectedUnitProperty); set => SetValue(SelectedUnitProperty, value); }

        public static readonly StyledProperty<DimNumber> DimNumberValueProperty = AvaloniaProperty.Register<UnitNumberBox, DimNumber>(nameof(DimNumberValue), null);
        public DimNumber DimNumberValue { get => GetValue(DimNumberValueProperty); set => SetValue(DimNumberValueProperty, value); }

        public ComboBox UnitComboBox { get; set; }

        public event EventHandler<NumberUnit> SelectedUnitChanged;

        public UnitNumberBox()
        {
            this.InitializeComponent();
        }

        private void OnUnitComboBoxLoaded(object sender, RoutedEventArgs e)
        {
            UnitComboBox = sender as ComboBox;
            UnitComboBox[!ComboBox.SelectedItemProperty] = new Avalonia.Data.Binding() { Source = this, Path = nameof(SelectedUnit), Mode = Avalonia.Data.BindingMode.TwoWay };
            UnitComboBox[!ComboBox.ItemsSourceProperty] = new Avalonia.Data.Binding() { Source = this, Path = nameof(Units) };
        }

        private void OnUnitComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedUnitChanged?.Invoke(this, SelectedUnit);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {
                var val = Value ?? 0m;
                if (DimNumberValue != null && DimNumberValue.Value != (double)val)
                    DimNumberValue = new DimNumber(SelectedUnit, (double)val);
            }
            else if (change.Property == SelectedUnitProperty)
            {
                if (DimNumberValue != null && DimNumberValue.Unit != SelectedUnit)
                    DimNumberValue = new DimNumber(SelectedUnit, (double)(Value ?? 0m));
            }
            else if (change.Property == DimNumberValueProperty)
            {
                if (DimNumberValue != null)
                {
                    Value = (decimal)DimNumberValue.Value;
                    SelectedUnit = DimNumberValue.Unit;
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Bindings?.StopTracking();
        }
    }
}

