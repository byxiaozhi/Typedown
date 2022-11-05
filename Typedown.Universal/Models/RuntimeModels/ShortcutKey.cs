using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace Typedown.Universal.Models
{
    public record ShortcutKey(VirtualKeyModifiers Modifiers, VirtualKey Key)
    {
        public override string ToString()
        {
            return base.ToString();
        }
    }
}
