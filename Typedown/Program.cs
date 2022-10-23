using System;
using System.Collections.Generic;
using System.Text;

namespace Typedown
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            using (new Universal.App())
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
