using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Interfaces
{
    public interface IFileExport
    {
        void HtmlToPdf(string basePath, string htmlString, string sourcePath, string savePath);

        void Print(string basePath, string htmlString);
    }
}
