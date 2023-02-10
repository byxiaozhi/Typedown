using System.Collections.Generic;

namespace Typedown.Core.Models
{
    public class ContentState
    {
        public WordCount WordCount { get; set; }

        public List<TocItem> Toc { get; set; }

        public TocItem Cur { get; set; }
    }
}
