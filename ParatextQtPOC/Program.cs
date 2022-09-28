using System;
using System.Text;
using Paratext.Data;
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

            int count = 0;
            unsafe
            {
                QApplication qtApp = new QApplication(ref count, null);
            }

            //MainWindow mainWindow = new MainWindow();
            //mainWindow.Show();

            TextEdit textEdit = new TextEdit();
            textEdit.Show();

            QApplication.Exec();
        }
    }
}
