using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using QtCore;
using QtCore.Qt;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    /// <summary>
    /// This is main application window
    /// More info here [QMainWindow](http://doc.qt.io/qt-5/qmainwindow.html#details)
    /// </summary>
    internal class TextEdit : QMainWindow{

        private QTextEdit textEdit;

        public TextEdit()
        {
            WindowTitle = "Test application";

            textEdit = new QTextEdit(this);

            CentralWidget = textEdit;
            Resize(640, 480);
        }
        
        public void LoadUsfm()
        {
            string fileName = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "/resources/19PSAwgPIDGIN.SFM";
            string dataString = File.ReadAllText(fileName);

            const bool rtl = false;

            QTextCharFormat markerFormat = new QTextCharFormat();
            markerFormat.Foreground = new QBrush(GlobalColor.Red);

            QTextCharFormat textFormat = new QTextCharFormat();
            textFormat.Font = new QFont("Calibri", rtl ? 36 : 12);

            Regex regex = new Regex(@"((\\x.*\*)|(\\[\w][\w\d]*[ \*\n\r])|([^\\]+))");

            QTextDocument doc = textEdit.Document;
            QTextCursor cursor = new QTextCursor(doc);

            QImage icon = new QImage(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "/resources/dice.png");

            Console.WriteLine("Starting loading");

            //cursor.BeginEditBlock(); // Using this slows down loading the application significantly: 13s -> 70s

            foreach (Match itemMatch in regex.Matches(dataString))
            {
                string matchString = itemMatch.ToString();
                if (matchString.Contains(@"\x"))
                {
                    cursor.InsertText("*", markerFormat);
                    // Do something with footnotes
                }
                else if (matchString.StartsWith('\\'))
                {
                    cursor.InsertText(rtl ? "\u200f" : "" + matchString, markerFormat);
                }
                else
                {
                    cursor.InsertImage(icon);
                    cursor.InsertText(matchString, textFormat);
                }
            }

            //cursor.EndEditBlock();

            Console.WriteLine("Finished loading");
        }
    }
}
