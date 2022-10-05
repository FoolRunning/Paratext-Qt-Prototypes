using System;
using System.Text;
using Paratext.Data;
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
        }
    }
}
