using System.Collections.Generic;
using Typedown.Core.Models;

namespace Typedown.Core.Utilities
{
    public static  class Units
    {
        public static NumberUnit Centimeter { get; } = new("cm", 1);

        public static NumberUnit Inch { get; } = new("in", 0.3937007874);

        public static IReadOnlyList<NumberUnit> LengthUnits { get; } = new List<NumberUnit>() { Centimeter, Inch };
    }
}
