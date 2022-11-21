using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Typedown.Utilities
{
    public static class PrintHelper
    {
        public static async Task PrintPDF(nint hWnd, Stream stream, string documentName = null)
        {
            await Task.Run(() =>
            {
                using var pdfDoc = PdfDocument.Load(stream);
                using var printDoc = pdfDoc.CreatePrintDocument();
                printDoc.DocumentName = documentName;
                using var dialog = new PrintDialog() { Document = printDoc, UseEXDialog = true };
                var result = dialog.ShowDialog(new Win32Window(hWnd));
                if (result == DialogResult.OK)
                    printDoc.Print();
            });
        }

        private record Win32Window(nint Handle) : IWin32Window;
    }
}
