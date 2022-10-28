using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Paratext.Data;
using QtCore;
using QtCore.Qt;
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
        private readonly DockWidgetArea[] location = new[]
        {
            QtCore.Qt.DockWidgetArea.RightDockWidgetArea, QtCore.Qt.DockWidgetArea.BottomDockWidgetArea,
            QtCore.Qt.DockWidgetArea.LeftDockWidgetArea, QtCore.Qt.DockWidgetArea.TopDockWidgetArea
        };

        //private readonly QLabel referenceLabel;
        private TextForm currentWindow;
        private bool referenceChanging;
        private TraceTimer timer;
        private bool alreadyPainted;
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
            
            Resize(1280, 720);

            timer = new TraceTimer(this);
            timer.Start(1000);
        }
        #endregion

        #region Properties
        public string TestCase;

        public static bool Async;

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

        private void Window_ReferenceChanged(object obj, ReferenceChangedArgs args)
        {
            if (referenceChanging)
                return;

            try
            {
                var sw = Stopwatch.StartNew();
                referenceChanging = true;
                TextForm window = (TextForm)(obj);
                int winCount = 0;
                foreach (var win in visibleWindows)
                {
                    if (win != window)
                    {
                        win.CurrentReference = args.NewVerseRef;
                        winCount++;
                    }
                }
                sw.Stop();
                if (winCount > 0)
                    Trace.TraceInformation($"Changing to {args.NewVerseRef} for {winCount} windows took {sw.ElapsedMilliseconds}ms");

            }
            finally
            {
                referenceChanging = false;
            }
        }
        
        private void BookSelector_CurrentIndexChanged(int index)
        {
            if (index == 0)
                return;

            int bookNum = bookSelector.ItemData(index).ToInt();
            OpenBook(bookNum, 1, 1);
        }

        private void OpenBook(int bookNum, int chapterNum, int verseNum)
        {
            Stopwatch sw = Stopwatch.StartNew();

            Window_ReferenceChanged(null,
                new ReferenceChangedArgs(new VerseRef(),
                    new VerseRef(bookNum, chapterNum, verseNum, currentWindow.ScrText.Settings.Versification)));
            //foreach (var win in visibleWindows)
            //   win.CurrentReference = new VerseRef(bookNum, 1, 1, currentWindow.ScrText.Settings.Versification);

            currentWindow.SetFocus(QtCore.Qt.FocusReason.OtherFocusReason);
            sw.Stop();
            Trace.TraceInformation(
                $"Loading {Canon.BookNumberToId(bookNum)} for {visibleWindows.Count} windows took {sw.ElapsedMilliseconds}ms");
        }

        private void SaveButton_Clicked(bool isChecked)
        {
            foreach (var win in visibleWindows)
                win.Save(win == currentWindow);

            // return focus to current window
            currentWindow?.SetFocus(FocusReason.OtherFocusReason);
        }

        private void Menu_Open(bool isChecked)
        {
            OpenProjectDialog dialog = new OpenProjectDialog(this);
            dialog.Exec();

            if (dialog.SelectedProjects != null)
            {
                foreach (var project in dialog.SelectedProjects) OpenProject(project);
            }
        }

        private void OpenProject(ScrText project)
        {
            Stopwatch sw = Stopwatch.StartNew();
            TextForm newWindow = new TextForm(project, this);
            sw.Stop();
            Trace.TraceInformation($"Creating TextForm for {project.Name} took {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            newWindow.AllowedAreas = CentralWidget == null
                ? QtCore.Qt.DockWidgetArea.NoDockWidgetArea
                : QtCore.Qt.DockWidgetArea.AllDockWidgetAreas;
            newWindow.FocusInEvent += (s, e) => CurrentWindow = (TextForm)s;
            newWindow.CloseEvent += Window_CloseEvent;
            newWindow.ReferenceChanged += Window_ReferenceChanged;
            visibleWindows.Add(newWindow);

            if (CentralWidget == null)
                CentralWidget = newWindow;
            else
                AddDockWidget(location[visibleWindows.Count % 4], newWindow);

            sw.Stop();
            Trace.TraceInformation($"Adding project {project.Name} to window took {sw.ElapsedMilliseconds}ms");
            CurrentWindow = newWindow;
        }

        private void Menu_Save(bool isChecked)
        {
            currentWindow?.Save(true);
        }

        private void Menu_Exit(bool isChecked)
        {
            Close();
        }

        protected override void OnPaintEvent(QPaintEvent @event)
        {
            base.OnPaintEvent(@event);
            if (alreadyPainted)
                return;
            alreadyPainted = true;
            if (string.IsNullOrEmpty(TestCase))
                return;

            Program.LogSinceStartTime($"Starting to create windows for test case: {TestCase}, Async = {Async}");
            string[] testResources = new[]
            {
                "NAV", "NIV84", "WEB", "RSV", "BENCLBSI", "HERV", "JCB", "VUL83", "HEB/GRK", "RVR1960", "ESVUS16", "CCB"
            };

            ScrText project;
            string[] parts = TestCase.Split("_");
            if (parts.Length < 2)
                return;

            if (parts[0].StartsWith("res-", StringComparison.OrdinalIgnoreCase))
            {
                int nbrResources = int.Parse(parts[0].Substring(4));
                foreach (var projectName in testResources.Take(nbrResources))
                {
                    project = ScrTextCollection.Find(projectName);
                    if (project == null)
                        continue;
                    OpenProject(project);
                }
            }
            else
            {
                project = ScrTextCollection.Find(parts[0]);
                if (project == null)
                    return;
                OpenProject(project);
            }
			
			int bookNum = Canon.BookIdToNumber(parts[1]);
			int chapterNum = parts.Length < 3 ? 1 : int.Parse(parts[2]);
			int verseNum = parts.Length < 4 ? 1 : int.Parse(parts[3]);
			OpenBook(bookNum, chapterNum, verseNum);
            Program.LogSinceStartTime($"Completed creating windows for test case: {TestCase}, Async = {Async}");
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

        #region TraceTimer class
        private class TraceTimer : QTimer
        {
            private DateTime lastTime;

            internal TraceTimer(QObject parent) : base(parent)
            {
                lastTime = DateTime.Now;
            }

            protected override void OnTimerEvent(QTimerEvent e)
            {
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - lastTime;
                Trace.TraceInformation($"It is now {now} and {elapsed.TotalSeconds:F3}secs passed since last tick");
                lastTime = now;
            }
        }
        #endregion
    }
}
