using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class DataEventArgs<T> : System.EventArgs
    {
        public T Data { get; }

        public DataEventArgs(T data)
        {
            Data = data;
        }
    }
}
