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
        public const int READONLY_TEXT_PROPERTY = 0x1000F5;
        public const int SPECIAL_VERSE = 1;
        public const int SPECIAL_FOOTNOTE_CALLER = 2;
        //public const int SPECIAL_ANNOTATION_ICON = 30;

        private readonly QTextBrowser textBrowser;
        private readonly QComboBox bookSelector;
        private readonly QComboBox projectSelector;
        private readonly QLabel referenceLabel;
        private readonly Dictionary<string, Annotation> annotationsInView = new Dictionary<string, Annotation>();
        private readonly List<AnnotationSource> annotationSources = new List<AnnotationSource>();
        private ScrText scrText;
        private int currentBook;

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

            textBrowser = new PTXTextBrowser(this);
            textBrowser.UndoRedoEnabled = true;
            textBrowser.ReadOnly = false;
            textBrowser.OpenLinks = false;
            textBrowser.OpenExternalLinks = false;
            textBrowser.TextInteractionFlags |= TextInteractionFlag.LinksAccessibleByMouse;
            textBrowser.Enabled = false;
            textBrowser.AnchorClicked += TextBrowserAnchorClicked;
            textBrowser.CursorPositionChanged += TextBrowserCursorPositionChanged;

            CentralWidget = textBrowser;
            Resize(1024, 768);
        }

        private void TextBrowserCursorPositionChanged()
        {
            QTextCursor cursor = textBrowser.TextCursor;
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
            if (textBrowser == null || index == 0)
                return; // Still initializing window

            foreach (var annotationSource in annotationSources)
                annotationSource.AnnotationsChanged -= AnnotationSource_AnnotationsChanged;

            annotationSources.Clear();

            scrText = ScrTextCollection.GetById(HexId.FromStr(projectSelector.ItemData(index).ToString()));

            annotationSources.Add(new NotesAnnotationSource(scrText));
            annotationSources.Add(new TranslationPromptsAnnotationSource(scrText));

            foreach (var annotationSource in annotationSources)
                annotationSource.AnnotationsChanged += AnnotationSource_AnnotationsChanged;

            WindowTitle = $"ParaNext™️ ({scrText.Name})";

            bookSelector.Clear();
            bookSelector.AddItem("Select book");
            foreach (int bookNum in scrText.Settings.BooksPresentSet.SelectedBookNumbers)
                bookSelector.AddItem(Canon.BookNumberToEnglishName(bookNum), bookNum);
            bookSelector.CurrentIndex = 0;
            bookSelector.Enabled = scrText.Settings.BooksPresentSet.Count > 0;
            textBrowser.LayoutDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textBrowser.Alignment = scrText.RightToLeft ? AlignmentFlag.AlignRight | AlignmentFlag.AlignAbsolute : AlignmentFlag.AlignLeft;
        }

        private void SaveButton_Clicked(bool isChecked)
        {
            using (TextWriter writer = new StreamWriter("./Temp.sfm"))
                WriteUsfm(writer);
        }

        private void WriteUsfm(TextWriter writer)
        {
            QTextBlock block = textBrowser.Document.Begin();
            while (block != textBrowser.Document.End())
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
                    else if (!fragment.CharFormat.HasProperty(IGNORE_FRAGMENT_PROPERTY) && (fragment.Text.Length > 1 || !fragment.Text.StartsWith(StringUtils.orcCharacter)))
                        writer.Write(fragment.Text);
                }

                writer.WriteLine();

                block = block.Next;
            }
        }

        private void TextBrowserAnchorClicked(QUrl linkUrl)
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
            if (textBrowser == null)
                return; // Still initializing window

            textBrowser.Document.Clear();
            textBrowser.Document.DefaultTextOption.TextDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textBrowser.Enabled = index > 0;
            if (index > 0)
            {
                int bookNum = bookSelector.ItemData(index).ToInt();
                LoadUsfm(bookNum);
            }
        }

        public void LoadUsfm(int bookNum)
        {
            currentBook = bookNum;
            Stopwatch sw = Stopwatch.StartNew();
            QTextDocument doc = textBrowser.Document;
            QTextCursor cursor = new QTextCursor(doc);
            Debug.Assert(cursor.AtStart);

            List<UsfmToken> tokens = scrText.Parser.GetUsfmTokens(bookNum);
            FormatText(bookNum, tokens, cursor);
            sw.Stop();
            Debug.WriteLine($"Loading {Canon.BookNumberToId(bookNum)} took {sw.ElapsedMilliseconds}ms");
        }

        private void FormatText(int bookNum, List<UsfmToken> tokens, QTextCursor cursor)
        {
            UsfmParser parser = new UsfmParser(scrText.ScrStylesheet(bookNum), tokens,
                new VerseRef(bookNum, 1, 0, scrText.Settings.Versification),
                new TextEditUsfmLoad(scrText, bookNum, cursor, annotationSources, annotationsInView));

            cursor.BeginEditBlock();
            parser.ProcessTokens();
            cursor.EndEditBlock();
        }

        private void AnnotationSource_AnnotationsChanged(object sender, AnnotationsChangedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            TextWriter writer = new StringWriter();
            WriteUsfm(writer);
            string currentUsfm = writer.ToString();
            List<UsfmToken> tokens = UsfmToken.Tokenize(scrText, currentBook, currentUsfm);
            QTextDocument newDoc = new QTextDocument();
            QTextCursor cursor = new QTextCursor(newDoc);
            FormatText(currentBook, tokens, cursor);
            QTextDocument curDoc = textBrowser.Document;
            if (newDoc.BlockCount != curDoc.BlockCount)
            {
                textBrowser.Document = newDoc;
                return;
            }

            for (int blockNbr = 0; blockNbr < newDoc.BlockCount; blockNbr++)
            {
                QTextBlock curBlock = curDoc.FindBlockByNumber(blockNbr);
                QTextBlock newBlock = newDoc.FindBlockByNumber(blockNbr);
                if (BlocksAreDifferent(curBlock, newBlock))
                    CopyBlock(curBlock, newBlock);

            }
            sw.Stop();
            Debug.WriteLine($"Refreshing {Canon.BookNumberToId(currentBook)} took {sw.ElapsedMilliseconds}ms");
        }

        private bool BlocksAreDifferent(QTextBlock curBlock, QTextBlock newBlock)
        {
            if (curBlock.Length != newBlock.Length ||
                !curBlock.BlockFormat.Equals(newBlock.BlockFormat) ||
                !curBlock.CharFormat.Equals(newBlock.CharFormat))
                return true;

            QTextBlock.Iterator curIter;
            QTextBlock.Iterator newIter;
            for (curIter = curBlock.Begin(), newIter = newBlock.Begin(); !curIter.AtEnd && !newIter.AtEnd; curIter++, newIter++)
            {
                QTextFragment curFragment = curIter.Fragment;
                QTextFragment newFragment = newIter.Fragment;

                if (curFragment.Position != newFragment.Position ||
                    curFragment.Length != newFragment.Length ||
                    curFragment.Text != newFragment.Text ||
                    !curFragment.CharFormat.Equals(newFragment.CharFormat))
                {
                    return true;
                }
            }

            return !curIter.AtEnd || !newIter.AtEnd;
        }

        private void CopyBlock(QTextBlock curBlock, QTextBlock newBlock)
        {
            curBlock.BlockFormat.Swap(newBlock.BlockFormat);
            curBlock.CharFormat.Swap(newBlock.CharFormat);
            QTextCursor deleteCursor = new QTextCursor(curBlock);
            deleteCursor.MovePosition(QTextCursor.MoveOperation.EndOfBlock, QTextCursor.MoveMode.KeepAnchor);
            deleteCursor.RemoveSelectedText();

            QTextCursor insertCursor = new QTextCursor(curBlock);
            insertCursor.BeginEditBlock();
            for (var newIter = newBlock.Begin(); !newIter.AtEnd; newIter++)
            {
                insertCursor.InsertText(newIter.Fragment.Text, newIter.Fragment.CharFormat);
            }
            insertCursor.EndEditBlock();
        }
    }
}
