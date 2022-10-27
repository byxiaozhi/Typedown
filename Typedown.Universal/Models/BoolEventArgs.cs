using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class BoolEventArgs : System.EventArgs
    {
        public bool Flag { get; }

        public BoolEventArgs(bool flag)
        {
            Flag = flag;
        }
    }
}
