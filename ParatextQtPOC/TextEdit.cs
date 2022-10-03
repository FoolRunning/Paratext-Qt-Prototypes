﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        #region Constants/Member variables
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
        #endregion

        #region Constructor
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
            textBrowser.AnchorClicked += TextBrowser_AnchorClicked;
            textBrowser.CursorPositionChanged += TextBrowser_CursorPositionChanged;

            CentralWidget = textBrowser;
            Resize(1024, 768);
        }
        #endregion

        #region Properties
        private VerseRef CurrentReference
        {
            get
            {
                QTextCursor cursor = textBrowser.TextCursor;
                return ReferenceFromBlock(cursor.Block, cursor.Position);
            }
        }
        #endregion

        #region Event handlers
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

        private void SaveButton_Clicked(bool isChecked)
        {
            using (TextWriter writer = new StreamWriter("./Temp.sfm"))
            {
                QTextBlock block = textBrowser.Document.Begin();
                while (block != textBrowser.Document.End())
                {
                    //string marker = block.BlockFormat.property(PARAGRAPH_MARKER_PROPERTY).ToString();
                    //writer.Write($"\\{marker} ");
                    writer.Write(UsfmForBlock(block));
                    writer.WriteLine();

                    block = block.Next;
                }
            }
        }

        private void TextBrowser_CursorPositionChanged()
        {
            referenceLabel.Text = $"Reference: ({CurrentReference.ToString() ?? "Unknown"})";
        }

        private void TextBrowser_AnchorClicked(QUrl linkUrl)
        {
            string[] parts = linkUrl.ToString().ToLowerInvariant().Split(':');
            if (parts[0] == "annotation" || parts[0] == "annotationicon")
            {
                string annotationId =  parts[1];
                Annotation annotationClicked = annotationsInView[annotationId];
                annotationClicked.Click(1, parts[0] == "annotationicon", null, new Coordinates(0, 0));
            }
        }

        private void AnnotationSource_AnnotationsChanged(object sender, AnnotationsChangedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();

            if (!e.RefreshAllAnnotations)
                RefreshViewForVerse(e.VerseRef);
            else
            {
                // ENHANCE: We could look at the number of verses affected and only do a full refresh if updating the verses individually would take too long
                LoadUsfm(currentBook);
            }

            sw.Stop();
            Debug.WriteLine($"Refreshing {Canon.BookNumberToId(currentBook)} took {sw.ElapsedMilliseconds}ms");
        }
        #endregion

        #region Private helper methods
        private void LoadUsfm(int bookNum)
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
            annotationsInView.Clear();
            UsfmParser parser = new UsfmParser(scrText.ScrStylesheet(bookNum), tokens,
                new VerseRef(bookNum, 1, 0, scrText.Settings.Versification),
                new TextEditUsfmLoad(scrText, bookNum, cursor, annotationSources, annotationsInView));

            cursor.BeginEditBlock();
            parser.ProcessTokens();
            cursor.EndEditBlock();
        }

        private void RefreshViewForVerse(VerseRef reference)
        {
            FindBlocksForReference(reference, out QTextBlock startBlock, out QTextBlock endBlock, out string usfm);
            if (startBlock == null || endBlock == null)
                return; // reference was not in the document

            // TODO: Remove annotations from annotationsInView that will get deleted

            VerseRef startRef = ReferenceFromBlock(startBlock, startBlock.Position);

            List<UsfmToken> tokens = UsfmToken.Tokenize(scrText, startRef.BookNum, usfm);

            int previousPosition = textBrowser.TextCursor.Position;
            QTextCursor cursor = new QTextCursor(startBlock);
            cursor.BeginEditBlock();

            cursor.MovePosition(QTextCursor.MoveOperation.Right, QTextCursor.MoveMode.KeepAnchor,
                endBlock.Position + endBlock.Length - startBlock.Position);
            cursor.RemoveSelectedText();
            Debug.Assert(cursor.Anchor == cursor.SelectionEnd);

            // Need to move to the end of the previous block to start inserting new blocks.
            // If we don't do that, the new blocks get added to the beginning of the current block
            // which has a side effect of creating a block that has nothing in it.
            cursor.MovePosition(QTextCursor.MoveOperation.PreviousBlock);
            cursor.MovePosition(QTextCursor.MoveOperation.EndOfBlock);

            UsfmParser parser = new UsfmParser(scrText.ScrStylesheet(startRef.BookNum), tokens,
                startRef, new TextEditUsfmLoad(scrText, startRef.BookNum, cursor, annotationSources, annotationsInView));
            parser.ProcessTokens();
            
            cursor.EndEditBlock();

            QTextCursor newCursor = new QTextCursor(textBrowser.Document);
            newCursor.MovePosition(QTextCursor.MoveOperation.Right, QTextCursor.MoveMode.MoveAnchor, previousPosition);
            textBrowser.TextCursor = newCursor;
        }

        private void FindBlocksForReference(VerseRef reference, out QTextBlock startBlock, out QTextBlock endBlock, out string containedUsfm)
        {
            startBlock = null;
            endBlock = null;
            QTextBlock block = textBrowser.Document.Begin();
            QTextBlock previousBlock = null;
            StringBuilder usfm = new StringBuilder();
            while (block != textBrowser.Document.End())
            {
                VerseRef blockRef = ReferenceFromBlock(block, block.Position);
                if (startBlock == null && blockRef.Equals(reference))
                {
                    startBlock = previousBlock;
                    usfm.AppendLine(UsfmForBlock(previousBlock));
                }

                if (blockRef > reference)
                {
                    if (startBlock == null)
                    {
                        startBlock = previousBlock;
                        usfm.AppendLine(UsfmForBlock(startBlock));
                    }

                    endBlock = previousBlock;
                    break;
                }

                if (startBlock != null)
                    usfm.AppendLine(UsfmForBlock(block));

                previousBlock = block;
                block = block.Next;
            }

            if (endBlock == null && startBlock != null) // Can happen if we hit the end of the document
                endBlock = previousBlock;

            Debug.Assert((startBlock == null && endBlock == null) || (startBlock != null && endBlock != null), "Not able to find consistent blocks");
            containedUsfm = usfm.ToString();
        }

        private static string UsfmForBlock(QTextBlock block)
        {
            StringBuilder strBldr = new StringBuilder();
            QTextBlock.Iterator iterator;
            for (iterator = block.Begin(); !iterator.AtEnd; iterator++)
            {
                QTextFragment fragment = iterator.Fragment;
                if (fragment.CharFormat.HasProperty(SPECIAL_PROPERTY))
                {
                    switch (fragment.CharFormat.property(SPECIAL_PROPERTY).ToInt())
                    {
                        case SPECIAL_VERSE:
                            strBldr.AppendLine();
                            strBldr.Append(fragment.Text);
                            break;
                        case SPECIAL_FOOTNOTE_CALLER:
                            strBldr.Append(fragment.CharFormat.ToolTip);
                            break;
                    }
                }
                else if (!fragment.CharFormat.HasProperty(IGNORE_FRAGMENT_PROPERTY) && (fragment.Text.Length > 1 || !fragment.Text.StartsWith(StringUtils.orcCharacter)))
                    strBldr.Append(fragment.Text);
            }

            return strBldr.ToString();
        }
        
        private VerseRef ReferenceFromBlock(QTextBlock block, int position)
        {
            string lastReference = null;
            QTextBlock.Iterator iterator;
            for (iterator = block.Begin(); !iterator.AtEnd; iterator++)
            {
                QTextFragment fragment = iterator.Fragment;
                if (fragment.Position > position)
                    break;
                
                if (fragment.CharFormat.HasProperty(VERSE_ID_PROPERTY))
                    lastReference = fragment.CharFormat.property(VERSE_ID_PROPERTY).ToString();
            }

            return lastReference != null ? new VerseRef(lastReference, scrText.Settings.Versification) : new VerseRef();
        }
        #endregion
    }
}
