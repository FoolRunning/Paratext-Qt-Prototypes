﻿using System;
using QtWidgets;

namespace ParatextQtPOC
{
    class Program
    {
        static void Main(string[] args)
        {
            int count = 0;
            unsafe
            {
                QApplication qtApp = new QApplication(ref count, null);
            }

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            QApplication.Exec();

            Console.WriteLine("Hello World!");
        }
    }
}
