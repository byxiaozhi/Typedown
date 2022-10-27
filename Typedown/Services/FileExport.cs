using System;
using System.Collections.Generic;
using System.Text;
using Typedown.Universal.Interfaces;

namespace Typedown.Services
{
    public class FileExport : IFileExport
    {
        public void HtmlToPdf(string basePath, string htmlString, string sourcePath, string savePath)
        {
            throw new NotImplementedException();
        }

        public void Print(string basePath, string htmlString)
        {
            throw new NotImplementedException();
        }
    }
}
