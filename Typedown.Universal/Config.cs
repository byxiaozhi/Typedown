using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal
{
    public static class Config
    {
        public static bool IsMicaEnable { get; } = Environment.OSVersion.Version.Build >= 22000;
    }
}
