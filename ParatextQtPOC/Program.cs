using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Paratext.Base;
using Paratext.Data;
using PtxUtils;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var logListener = new LogTraceListener(Path.Combine(Environment.CurrentDirectory, "ParatextQtPoc.log"), 10 * 1024 * 1024);
            Trace.Listeners.Add(logListener);
            Trace.AutoFlush = true; // will often just kill program, so want to always write to disk.
            LogSinceStartTime("Starting proof of concept for QT");

            ScrTextCollection.Implementation = new ParatextScrTextCollection(null, null);
            ParatextData.Initialize();
            
            QApplication.Style = new ParatextQtStyle();
            QGuiApplication.Palette = QApplication.Style.StandardPalette;

            LogSinceStartTime("Completed ParatextData initialization");
            int count = 0;
            unsafe
            {
                QApplication qtApp = new QApplication(ref count, null);
            }

            MainWindow textEdit = new MainWindow();
            if (args.Length > 0)
            {
                textEdit.TestCase = args[0];
                if (args.Length >= 2 && args[1].Equals("async", StringComparison.OrdinalIgnoreCase))
                    MainWindow.Async = true;
            }

            textEdit.Show();

            QApplication.Exec();
            LogSinceStartTime("Closing application");
            Trace.Close();
        }

        public static void LogSinceStartTime(string message)
        {
            TimeSpan sinceStartTime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            Trace.TraceInformation($"Since process start: {sinceStartTime.TotalSeconds:F3}, {message}");
        }
    }
}
