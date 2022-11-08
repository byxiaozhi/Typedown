using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class ScrollState
    {
        public double ViewportWidth { get; set; }
        public double ViewportHeight { get; set; }
        public double MaximumX { get; set; }
        public double MaximumY { get; set; }
        public double ScrollX { get; set; }
        public double ScrollY { get; set; }
    }
}
