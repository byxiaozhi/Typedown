using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Models
{
    public record DimNumber
    {
        public NumberUnit Unit { get; set; }

        public double Value { get; set; }

        public DimNumber()
        {

        }

        public DimNumber(NumberUnit unit, double value = 0)
        {
            Unit = unit;
            Value = value;
        }

        public double GetValue(NumberUnit targetUnit)
        {
            return ((Value - Unit.Shift) / Unit.Scale) * targetUnit.Scale + targetUnit.Shift;
        }
    }
}
