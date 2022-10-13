using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Paratext.Data;
using PtxUtils;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var logListener = new LogTraceListener(Path.Combine(Environment.CurrentDirectory, "ParatextQtPoc.log"), 10 * 1024 * 1024);
            Trace.Listeners.Add(logListener);
            Trace.AutoFlush = true; // will often just kill program, so want to always write to disk.
            Trace.TraceInformation("Starting proof of concept for QT");

            ParatextData.Initialize();
            
            QApplication.Style = new ParatextQtStyle();
            QGuiApplication.Palette = QApplication.Style.StandardPalette;

            int count = 0;
            unsafe
            {
                QApplication qtApp = new QApplication(ref count, null);
            }

            MainWindow textEdit = new MainWindow();
            textEdit.Show();

            QApplication.Exec();
            Trace.Close();
        }
    }
}
