using System;
using System.Collections.Generic;
using System.IO;
using Paratext.Data;
using QtCore;
using QtGui;
using QtWidgets;
using SIL.Scripture;

namespace ParatextQtPOC
{
    /// <summary>
    /// This is main application window
    /// More info here [QMainWindow](http://doc.qt.io/qt-5/qmainwindow.html#details)
    /// </summary>
    internal class MainWindow : QMainWindow
    {
        #region Constants/Member variables
        private readonly List<TextForm> visibleWindows = new List<TextForm>();
        private readonly QComboBox bookSelector;
        private readonly QComboBox projectSelector;
        //private readonly QLabel referenceLabel;
        private TextForm currentWindow;
        #endregion

        #region Constructor
        public MainWindow()
        {
            WindowTitle = "ParaNext™️";
            DockOptions = DockOption.AllowNestedDocks | DockOption.AllowTabbedDocks;
            WindowIcon = new QIcon(Path.Combine(Environment.CurrentDirectory, "resources", "Icon.png"));
            
            QToolBar toolbar = new QToolBar();
            toolbar.Floatable = false;
            toolbar.Movable = false;

            QFont boldFont = new QFont("Arial", 11, (int)QFont.Weight.Bold);
            QToolButton menuButton = new QToolButton(this);
            menuButton.PopupMode = QToolButton.ToolButtonPopupMode.InstantPopup;
            menuButton.Icon = new QIcon(Path.Combine(Environment.CurrentDirectory, "resources", "hamburger_black_24.png"));
            menuButton.Menu = CreateMainMenu();
            menuButton.BaseSize = new QSize(45, 45);
            menuButton.SetContentsMargins(1, 1, 1, 1);
            toolbar.AddWidget(menuButton);
            
            projectSelector = new QComboBox();
            projectSelector.SetContentsMargins(3, 3, 3, 3);
            projectSelector.Font = boldFont;
            projectSelector.View.MinimumWidth = 500;
            //projectSelector.SizePolicy = new QSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Preferred);
            projectSelector.MaximumSize = new QSize(200, 100);
            projectSelector.AddItem("Select project");
            foreach (ScrText scr in ScrTextCollection.ScrTexts(IncludeProjects.ScriptureOnly))
                projectSelector.AddItem(scr.ToString(), scr.Guid.ToString());
            projectSelector.CurrentIndex = 0;
            projectSelector.CurrentIndexChanged += ProjectSelector_CurrentIndexChanged;
            toolbar.AddWidget(projectSelector);

            bookSelector = new QComboBox();
            bookSelector.SetContentsMargins(3, 3, 3, 3);
            bookSelector.Font = boldFont;
            bookSelector.AddItem("Select book");
            foreach (int bookNum in Canon.AllBookNumbers)
                bookSelector.AddItem(Canon.BookNumberToId(bookNum) + " - " + Canon.BookNumberToEnglishName(bookNum));
            bookSelector.CurrentIndex = 0;
            bookSelector.Enabled = false;
            toolbar.AddWidget(bookSelector);

            QPushButton saveButton = new QPushButton("Save");
            saveButton.Font = boldFont;
            saveButton.Clicked += SaveButton_Clicked;
            toolbar.AddWidget(saveButton);

            //referenceLabel = new QLabel();
            //referenceLabel.Font = new QFont("Arial", 15);
            //referenceLabel.Text = "Reference: ()";
            //toolbar.AddWidget(referenceLabel);

            AddToolBar(toolbar);
            
            Resize(1024, 768);
        }
        #endregion

        #region Properties
        private TextForm CurrentWindow
        {
            get => currentWindow;
            set
            {
                if (currentWindow == value) 
                    return;

                currentWindow = value;

                bookSelector.CurrentIndexChanged -= BookSelector_CurrentIndexChanged;
                bookSelector.Clear();
                bookSelector.AddItem("Select book");
                if (value == null)
                    bookSelector.CurrentIndex = 0;
                else
                {
                    VerseRef currentRef = value.CurrentReference;
                    foreach (int bookNum in value.ScrText.Settings.BooksPresentSet.SelectedBookNumbers)
                        bookSelector.AddItem(Canon.BookNumberToId(bookNum) + " - " + Canon.BookNumberToEnglishName(bookNum), bookNum);
                    bookSelector.CurrentIndex = currentRef.BookNum == 0 ? 0 : bookSelector.FindData(currentRef.BookNum);
                }

                bookSelector.Enabled = value?.ScrText != null && value?.ScrText.Settings.BooksPresentSet.Count > 0;
                bookSelector.CurrentIndexChanged += BookSelector_CurrentIndexChanged;

                projectSelector.CurrentIndex = projectSelector.FindData(value?.ScrText.Guid.ToString());
                projectSelector.Enabled = value != null;
            }
        }

        //private VerseRef CurrentReference
        //{
        //    get => currentWindow?.CurrentReference ?? new VerseRef();
        //    set
        //    {
                
        //    }
        //}
        #endregion

        #region Event handlers
        private void ProjectSelector_CurrentIndexChanged(int index)
        {
            if (index == 0 || currentWindow == null)
                return;

            ScrText scrText = ScrTextCollection.GetById(HexId.FromStr(projectSelector.ItemData(index).ToString()));
            currentWindow.ChangeProject(scrText);
        }

        private void Window_CloseEvent(object obj, QCloseEvent arg2)
        {
            TextForm window = (TextForm)obj;
            if (CurrentWindow == window)
                CurrentWindow = null;

            visibleWindows.Remove(window);
        }
        
        private void BookSelector_CurrentIndexChanged(int index)
        {
            if (index == 0)
                return;

            int bookNum = bookSelector.ItemData(index).ToInt();
            currentWindow.LoadBook(bookNum);
        }

        private void SaveButton_Clicked(bool isChecked)
        {
            currentWindow?.Save();
        }

        private void Menu_Open(bool isChecked)
        {
            OpenProjectDialog dialog = new OpenProjectDialog();
            dialog.Exec();

            if (dialog.SelectedProject != null)
            {
                TextForm newWindow = new TextForm(dialog.SelectedProject, this);
                newWindow.AllowedAreas = CentralWidget == null ? QtCore.Qt.DockWidgetArea.NoDockWidgetArea : QtCore.Qt.DockWidgetArea.AllDockWidgetAreas;
                newWindow.FocusInEvent += (s, e) => CurrentWindow = (TextForm)s;
                newWindow.CloseEvent += Window_CloseEvent;
                visibleWindows.Add(newWindow);

                if (CentralWidget == null)
                    CentralWidget = newWindow;
                else
                    AddDockWidget(QtCore.Qt.DockWidgetArea.RightDockWidgetArea, newWindow);

                CurrentWindow = newWindow;
            }
        }
        
        private void Menu_Save(bool isChecked)
        {
            currentWindow?.Save();
        }

        private void Menu_Exit(bool isChecked)
        {
            Close();
        }
        #endregion

        #region Private helper methods

        private QMenu CreateMainMenu()
        {
            QMenu menu = new QMenu();
            menu.SetContentsMargins(0, 0, 0, 0);

            QWidgetAction contents = new QWidgetAction(menu);
            QWidget widgetContents = new QWidget(menu);
            widgetContents.SetContentsMargins(0, 0, 0, 0);
            QGridLayout layout = new QGridLayout(widgetContents);
            layout.SetContentsMargins(0, 0, 0, 0);
            widgetContents.Layout = layout;
            
            layout.AddWidget(CreateMenuButton("&Open", Menu_Open), 0, 0);
            layout.AddWidget(CreateMenuButton("&Save", Menu_Save), 1, 0);
            layout.AddWidget(CreateMenuButton("E&xit", Menu_Exit), 2, 0);

            for (int column = 1; column < 5; column++)
            {
                layout.AddWidget(CreateMenuButton("Click me 1"), 0, column);
                layout.AddWidget(CreateMenuButton("Click me 2"), 1, column);
                layout.AddWidget(CreateMenuButton("Click me 3"), 2, column);
                layout.AddWidget(CreateMenuButton("Click me 4124153155 513553"), 3, column);
            }

            contents.DefaultWidget = widgetContents;
            menu.AddAction(contents);

            return menu;
        }

        private static QPushButton CreateMenuButton(string text, Action<bool> clickHandler = null)
        {
            QPushButton menuButton = new QPushButton(text);
            if (clickHandler != null)
                menuButton.Clicked += clickHandler;
            return menuButton;
        }
        #endregion
    }
}
