using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Paratext.Data;
using PtxUtils;
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
    internal class TextEdit : QMainWindow
    {
        public const int SPECIAL_PROPERTY = 0x1000F1;
        public const int VERSE_ID_PROPERTY = 0x1000F2;
        public const int PARAGRAPH_MARKER_PROPERTY = 0x1000F3;
        public const int IGNORE_FRAGMENT_PROPERTY = 0x1000F4;
        public const int SPECIAL_VERSE = 1;
        public const int SPECIAL_FOOTNOTE_CALLER = 2;
        //public const int SPECIAL_ANNOTATION_ICON = 30;

        private readonly QTextBrowser textEdit;
        private readonly QComboBox bookSelector;
        private readonly QComboBox projectSelector;
        private readonly QLabel referenceLabel;
        private readonly Dictionary<string, Annotation> annotationsInView = new Dictionary<string, Annotation>();
        private ScrText scrText;

        public TextEdit()
        {
            WindowTitle = $"ParaNext™️";
            QToolBar toolbar = new QToolBar();
            toolbar.Floatable = false;
            toolbar.Movable = false;

            projectSelector = new QComboBox();
            projectSelector.Font = new QFont("Arial", 12);
            projectSelector.AddItem("Select project");
            foreach (ScrText scr in ScrTextCollection.ScrTexts(IncludeProjects.ScriptureOnly))
                projectSelector.AddItem(scr.ToString(), scr.Guid.ToString());
            projectSelector.CurrentIndex = 0;
            projectSelector.CurrentIndexChanged += ProjectSelector_CurrentIndexChanged;
            toolbar.AddWidget(projectSelector);

            bookSelector = new QComboBox();
            bookSelector.Font = new QFont("Arial", 12);
            bookSelector.AddItem("Select book");
            bookSelector.CurrentIndex = 0;
            bookSelector.Enabled = false;
            bookSelector.CurrentIndexChanged += BookSelector_CurrentIndexChanged;
            toolbar.AddWidget(bookSelector);

            QPushButton saveButton = new QPushButton("Save");
            saveButton.Clicked += SaveButton_Clicked;
            toolbar.AddWidget(saveButton);

            referenceLabel = new QLabel();
            referenceLabel.Font = new QFont("Arial", 15);
            referenceLabel.Text = "Reference: ()";
            toolbar.AddWidget(referenceLabel);

            AddToolBar(toolbar);

            textEdit = new QTextBrowser(this);
            textEdit.UndoRedoEnabled = true;
            textEdit.ReadOnly = false;
            textEdit.OpenLinks = false;
            textEdit.OpenExternalLinks = false;
            textEdit.TextInteractionFlags |= TextInteractionFlag.LinksAccessibleByMouse;
            textEdit.Enabled = false;
            textEdit.AnchorClicked += TextEdit_AnchorClicked;
            textEdit.CursorPositionChanged += TextEdit_CursorPositionChanged;

            CentralWidget = textEdit;
            Resize(1024, 768);
        }

        private void TextEdit_CursorPositionChanged()
        {
            QTextCursor cursor = textEdit.TextCursor;
            string lastReference = null;
            QTextBlock block = cursor.Block;
            QTextBlock.Iterator iterator;
            for (iterator = block.Begin(); !iterator.AtEnd; iterator++)
            {
                QTextFragment fragment = iterator.Fragment;
                if (fragment.Position > cursor.Position)
                    break;
                
                if (fragment.CharFormat.HasProperty(VERSE_ID_PROPERTY))
                    lastReference = fragment.CharFormat.property(VERSE_ID_PROPERTY).ToString();
            }

            referenceLabel.Text = $"Reference: ({lastReference ?? "Unknown"})";
        }

        private void ProjectSelector_CurrentIndexChanged(int index)
        {
            if (textEdit == null || index == 0)
                return; // Still initializing window

            scrText = ScrTextCollection.GetById(HexId.FromStr(projectSelector.ItemData(index).ToString()));
            WindowTitle = $"ParaNext™️ ({scrText.Name})";

            bookSelector.Clear();
            bookSelector.AddItem("Select book");
            foreach (int bookNum in scrText.Settings.BooksPresentSet.SelectedBookNumbers)
                bookSelector.AddItem(Canon.BookNumberToEnglishName(bookNum), bookNum);
            bookSelector.CurrentIndex = 0;
            bookSelector.Enabled = scrText.Settings.BooksPresentSet.Count > 0;
            textEdit.LayoutDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textEdit.Alignment = scrText.RightToLeft ? AlignmentFlag.AlignRight | AlignmentFlag.AlignAbsolute : AlignmentFlag.AlignLeft;
        }

        private void SaveButton_Clicked(bool isChecked)
        {
            using (TextWriter writer = new StreamWriter("./Temp.sfm"))
            {
                QTextBlock block = textEdit.Document.Begin();
                while (block != textEdit.Document.End())
                {
                    //string marker = block.BlockFormat.property(PARAGRAPH_MARKER_PROPERTY).ToString();
                    //writer.Write($"\\{marker} ");

                    QTextBlock.Iterator iterator;
                    for (iterator = block.Begin(); !iterator.AtEnd; iterator++)
                    {
                        QTextFragment fragment = iterator.Fragment;
                        if (fragment.CharFormat.HasProperty(SPECIAL_PROPERTY))
                        {
                            switch (fragment.CharFormat.property(SPECIAL_PROPERTY).ToInt())
                            {
                                case SPECIAL_VERSE: 
                                    writer.WriteLine();
                                    writer.Write(fragment.Text);
                                    break;
                                case SPECIAL_FOOTNOTE_CALLER:
                                    writer.Write(fragment.CharFormat.ToolTip);
                                    break;
                            }
                        }
                        else if (/*!fragment.CharFormat.HasProperty(IGNORE_FRAGMENT_PROPERTY) &&*/ (fragment.Text.Length > 1 || !fragment.Text.StartsWith(StringUtils.orcCharacter)))
                            writer.Write(fragment.Text);
                    }
                    
                    writer.WriteLine();

                    block = block.Next;
                }
            }
        }

        private void TextEdit_AnchorClicked(QUrl linkUrl)
        {
            string[] parts = linkUrl.ToString().ToLowerInvariant().Split(':');
            if (parts[0] == "annotation" || parts[0] == "annotationicon")
            {
                string annotationId =  parts[1];
                Annotation annotationClicked = annotationsInView[annotationId];
                annotationClicked.Click(1, parts[0] == "annotationicon", null, new Coordinates(0, 0));
            }
        }

        private void BookSelector_CurrentIndexChanged(int index)
        {
            if (textEdit == null)
                return; // Still initializing window

            textEdit.Document.Clear();
            textEdit.Document.DefaultTextOption.TextDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textEdit.Enabled = index > 0;
            if (index > 0)
            {
                int bookNum = bookSelector.ItemData(index).ToInt();
                LoadUsfm(bookNum);
            }
        }

        public void LoadUsfm(int bookNum)
        {
            AnnotationSource[] annotationSources = { new NotesAnnotationSource(scrText) };
            
            Stopwatch sw = Stopwatch.StartNew();
            QTextDocument doc = textEdit.Document;
            QTextCursor cursor = new QTextCursor(doc);
            Debug.Assert(cursor.AtStart);

            List<UsfmToken> tokens = scrText.Parser.GetUsfmTokens(bookNum);
            UsfmParser parser = new UsfmParser(scrText.ScrStylesheet(bookNum), tokens,
                new VerseRef(bookNum, 1, 0, scrText.Settings.Versification), 
                new TextEditUsfmLoad(scrText, bookNum, cursor, annotationSources, annotationsInView));

            cursor.BeginEditBlock();
            parser.ProcessTokens();
            cursor.EndEditBlock();

            sw.Stop();
            Debug.WriteLine($"Loading {Canon.BookNumberToId(bookNum)} took {sw.ElapsedMilliseconds}ms");
        }
    }
}
