using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Paratext.Data.Themes;
using QtCore;
using QtCore.Qt;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    internal sealed class TitleBar : QWidget
    {
        private readonly QLabel titleLabel;

        public TitleBar(QDockWidget parent, string initialText) : base(parent)
        {
            parent.WindowTitleChanged += Parent_WindowTitleChanged;

            QGridLayout layout = new QGridLayout(this);
            layout.SetContentsMargins(3, 3, 1, 1);
            layout.SetColumnStretch(1, 100);
            Layout = layout;

            QToolButton menuButton = new QToolButton(this);
            menuButton.PopupMode = QToolButton.ToolButtonPopupMode.InstantPopup;
            menuButton.Icon = new QIcon(Path.Combine(Environment.CurrentDirectory, "resources", "hamburger_black_24.png"));
            menuButton.Menu = CreateTestMenu();
            //menuButton.Flat = true;
            menuButton.BaseSize = new QSize(25, 25);
            menuButton.SetContentsMargins(1, 1, 1, 1);
            layout.AddWidget(menuButton, 0, 0, AlignmentFlag.AlignLeft);

            titleLabel = new QLabel(this);
            titleLabel.Font = new QFont("Arial", 12);
            titleLabel.Text = initialText;
            layout.AddWidget(titleLabel, 0, 1, AlignmentFlag.AlignLeft);
        }

        private QMenu CreateTestMenu()
        {
            QMenu menu = new QMenu();
            menu.SetContentsMargins(0, 0, 0, 0);

            QWidgetAction contents = new QWidgetAction(menu);
            QWidget widgetContents = new QWidget(menu);
            widgetContents.SetContentsMargins(0, 0, 0, 0);
            QGridLayout layout = new QGridLayout(widgetContents);
            layout.SetContentsMargins(0, 0, 0, 0);
            widgetContents.Layout = layout;
            for (int column = 0; column < 5; column++)
            {
                layout.AddWidget(new QPushButton("Click me 1"), 0, column);
                layout.AddWidget(new QPushButton("Click me 2"), 1, column);
                layout.AddWidget(new QPushButton("Click me 3"), 2, column);
                layout.AddWidget(new QPushButton("Click me 4124153155 513553"), 3, column);
            }

            contents.DefaultWidget = widgetContents;
            menu.AddAction(contents);

            return menu;
        }

        private void Parent_WindowTitleChanged(string newTitle)
        {
            titleLabel.Text = newTitle;
        }
    }
}
