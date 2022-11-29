using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Models;

namespace Typedown.Universal.Utilities
{
    public static  class Units
    {
        public static NumberUnit Centimeter { get; } = new("cm", 1);

        public static NumberUnit Inch { get; } = new("in", 0.3937007874);

        public static IReadOnlyList<NumberUnit> LengthUnits { get; } = new List<NumberUnit>() { Centimeter, Inch };
    }
}
