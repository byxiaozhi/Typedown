using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public struct NumberUnit
    {
        public string Name { get; }

        public double Scale { get; }

        public double Shift { get; }

        public NumberUnit(string name, double scale, double shift = 0)
        {
            Name = name;
            Scale = scale;
            Shift = shift;
        }

        public override bool Equals(object obj) => obj is NumberUnit other && Equals(other);

        public bool Equals(NumberUnit unit) => unit.Name == Name && unit.Scale == Scale && unit.Shift == Shift;

        public override int GetHashCode() => HashCode.Combine(Name, Scale, Shift);

        public static bool operator ==(NumberUnit lhs, NumberUnit rhs) => lhs.Equals(rhs);

        public static bool operator !=(NumberUnit lhs, NumberUnit rhs) => !(lhs == rhs);
    }
}
