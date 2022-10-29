using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Typedown.Universal.Models
{
    public class WindowPlacement
    {
        public bool IsMaximized { get;  }

        public Rect NormalPosition { get;  }

        public WindowPlacement(bool isMaximized, Rect normalPosition)
        {
            IsMaximized = isMaximized;
            NormalPosition = normalPosition;
        }
    }
}
