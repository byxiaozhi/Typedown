using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class ContentState
    {
        public WordCount WordCount { get; set; }

        public List<TocItem> Toc { get; set; }

        public TocItem Cur { get; set; }
    }
}
