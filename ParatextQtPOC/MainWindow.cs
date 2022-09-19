using System;
using System.Collections.Generic;
using System.Text;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    /// <summary>
    /// This is main application window
    /// More info here [QMainWindow](http://doc.qt.io/qt-5/qmainwindow.html#details)
    /// </summary>
    public class MainWindow : QMainWindow
    {
        public MainWindow()
        {
            WindowTitle = "Test application";

            // NOTE : Central widget is required !
            QPushButton button = new QPushButton("Don't click on me!");
            button.Font = new QFont("Arial", 30);
            button.Clicked += Button_Clicked;

            CentralWidget = button;
            Resize(640, 480);
        }

        private void Button_Clicked(bool obj)
        {
            QMessageBox.Information(this, "Why? Why? Why?", "Why did you click on me after I specifically asked you not to?!", QMessageBox.StandardButton.Close);
        }
    }
}
