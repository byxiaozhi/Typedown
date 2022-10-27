using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class ReactiveEventArgs : System.EventArgs
    {
        public Type DeclaringType { get; }
        public string PropertyName { get; }

        public ReactiveEventArgs(Type declaringType, string propertyName)
        {
            DeclaringType = declaringType;
            PropertyName = propertyName;
        }
    }
}
