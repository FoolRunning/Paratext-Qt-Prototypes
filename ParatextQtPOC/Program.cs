using System;
using Paratext.Data;
using QtWidgets;

namespace ParatextQtPOC
{
    class Program
    {
        static void Main(string[] args)
        {
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
            textEdit.LoadUsfm();

            QApplication.Exec();
        }
    }
}
