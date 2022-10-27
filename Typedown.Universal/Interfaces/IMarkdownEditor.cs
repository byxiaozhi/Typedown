using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Interfaces
{
    public interface IMarkdownEditor
    {
        void PostMessage(string name, object arg);
    }
}
