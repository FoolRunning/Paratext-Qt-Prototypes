using Paratext.Data;
using PtxUtils;
using QtCore.Qt;
using QtCore;
using QtGui;
using QtWidgets;
using SIL.Scripture;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ParatextQtPOC
{
    public class ReferenceChangedArgs
    {
        public readonly VerseRef PreviousVerseRef;
        public readonly VerseRef NewVerseRef;

        public ReferenceChangedArgs(VerseRef prevRef, VerseRef newRef)
        {
            PreviousVerseRef = prevRef;
            NewVerseRef = newRef;
        }
    }

    internal class TextForm : QDockWidget
    {
        #region Constants/Member variables
        private readonly QTextBrowser textBrowser;
        private readonly Dictionary<string, Annotation> annotationsInView = new Dictionary<string, Annotation>();
        private readonly List<AnnotationSource> annotationSources = new List<AnnotationSource>();
        private ScrText scrText;
        private int currentBook;
        private VerseRef lastReference;
        private bool loadingText;
        #endregion

        #region Constructor
        public TextForm(ScrText scrText, QWidget parent) : base("Temp", parent)
        {
            TitleBarWidget = new TitleBar(this, "Temp");
            FocusPolicy = FocusPolicy.StrongFocus;

            textBrowser = new PTXTextBrowser(null);
            textBrowser.UndoRedoEnabled = true;
            textBrowser.ReadOnly = false;
            textBrowser.OpenLinks = false;
            textBrowser.OpenExternalLinks = false;
            textBrowser.TextInteractionFlags |= TextInteractionFlag.LinksAccessibleByMouse;
            textBrowser.Enabled = false;
            textBrowser.AnchorClicked += TextBrowser_AnchorClicked;
            textBrowser.CursorPositionChanged += TextBrowser_CursorPositionChanged;
            Widget = textBrowser;

            Resize(1024, 768);

            ChangeProject(scrText);
        }
        #endregion

        #region Properties

        public ScrText ScrText
        {
            get => scrText;
            set => ChangeProject(value);
        }

        public VerseRef CurrentReference
        {
            get
            {
                QTextCursor cursor = textBrowser.TextCursor;
                return ReferenceFromBlock(cursor.Block, cursor.Position);
            }

            set
            {
                if (currentBook != value.BookNum)
                    LoadBook(value.BookNum);
                ScrollVerseIntoView(value);
                UpdateWindowTitle();
            }
        }

        public event EventHandler<ReferenceChangedArgs> ReferenceChanged;
        #endregion
        
        public void ChangeProject(ScrText newScrText)
        {
            if (scrText == newScrText)
                return;

            foreach (AnnotationSource annotationSource in annotationSources)
                annotationSource.AnnotationsChanged -= AnnotationSource_AnnotationsChanged;

            annotationSources.Clear();

            scrText = newScrText;

            annotationSources.Add(new NotesAnnotationSource(scrText));
            annotationSources.Add(new TranslationPromptsAnnotationSource(scrText));

            foreach (var annotationSource in annotationSources)
                annotationSource.AnnotationsChanged += AnnotationSource_AnnotationsChanged;

            textBrowser.LayoutDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textBrowser.Alignment = scrText.RightToLeft ? AlignmentFlag.AlignRight | AlignmentFlag.AlignAbsolute : AlignmentFlag.AlignLeft;

            UpdateWindowTitle();

            if (currentBook != 0)
                LoadBook(currentBook);
        }

        public void LoadBook(int bookNum)
        {
            if (textBrowser == null)
                return; // Still initializing window

            textBrowser.Document.Clear();
            textBrowser.Document.DefaultTextOption.TextDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            textBrowser.Enabled = bookNum > 0;
            if (bookNum > 0)
                LoadUsfm(bookNum);
        }

        public void Save()
        {
            // TODO: Allow user to select file location
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

        protected override void OnFocusInEvent(QFocusEvent @event)
        {
            base.OnFocusInEvent(@event);
            textBrowser.SetFocus(FocusReason.OtherFocusReason);
        }

        #region Event handlers
        private void TextBrowser_CursorPositionChanged()
        {
            if (loadingText)
                return;

            UpdateWindowTitle();
            VerseRef newReference = CurrentReference;
            if (lastReference.IsDefault || !lastReference.Equals(newReference))
            {
                ReferenceChanged?.Invoke(this, new ReferenceChangedArgs(lastReference, newReference));
                lastReference = newReference;
            }
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
        private void UpdateWindowTitle()
        {
            WindowTitle = $"{scrText.Name}: {CurrentReference} (Editable)";
        }

        private void LoadUsfm(int bookNum)
        {
            currentBook = bookNum;
            loadingText = true;

            Stopwatch sw = Stopwatch.StartNew();
            
            QTextDocument doc = textBrowser.Document;
            QTextCursor cursor = new QTextCursor(doc);
            Debug.Assert(cursor.AtStart);

            List<UsfmToken> tokens = scrText.Parser.GetUsfmTokens(bookNum);
            FormatText(bookNum, tokens, cursor);
            loadingText = false;
            CurrentReference = new VerseRef(currentBook, 1, 1, scrText.Settings.Versification);
            
            sw.Stop();
            Debug.WriteLine($"Loading {Canon.BookNumberToId(bookNum)} from {scrText.Name} took {sw.ElapsedMilliseconds}ms");
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
                if (fragment.CharFormat.HasProperty(TextEditUsfmLoad.SPECIAL_PROPERTY))
                {
                    switch (fragment.CharFormat.property(TextEditUsfmLoad.SPECIAL_PROPERTY).ToInt())
                    {
                        case TextEditUsfmLoad.SPECIAL_VERSE:
                            strBldr.AppendLine();
                            strBldr.Append(fragment.Text);
                            break;
                        case TextEditUsfmLoad.SPECIAL_FOOTNOTE_CALLER:
                            strBldr.Append(fragment.CharFormat.ToolTip);
                            break;
                    }
                }
                else if (!fragment.CharFormat.HasProperty(TextEditUsfmLoad.IGNORE_FRAGMENT_PROPERTY) && (fragment.Text.Length > 1 || !fragment.Text.StartsWith(StringUtils.orcCharacter)))
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
                
                if (fragment.CharFormat.HasProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY))
                    lastReference = fragment.CharFormat.property(TextEditUsfmLoad.VERSE_ID_PROPERTY).ToString();
            }

            return lastReference != null ? new VerseRef(lastReference, scrText.Settings.Versification) : new VerseRef();
        }

        private void ScrollVerseIntoView(VerseRef verseRef)
        {
            verseRef.ChangeVersification(scrText.Settings.Versification);
            QTextFragment verseFragment = null;
            var block = textBrowser.Document.Begin();
            for (; verseFragment == null && block != textBrowser.Document.End(); block = block.Next)
            {
                for (var iter = block.Begin(); !iter.AtEnd; iter++)
                {
                    var fragment = iter.Fragment;
                    if (fragment.CharFormat.HasProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY))
                    {
                        var fragmentVerseRef = new VerseRef(fragment.CharFormat.StringProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY), scrText.Settings.Versification);
                        if (verseRef.OverlapsAny(fragmentVerseRef))
                        {
                            verseFragment = fragment;
                            break;
                        }
                    }
                }
            }

            if (verseFragment == null)
                return;

            QTextCursor cursor = new QTextCursor(block);
            cursor.SetPosition(verseFragment.Position + verseFragment.Length + verseRef.Verse.Length + 1);
            textBrowser.TextCursor = cursor;
            textBrowser.EnsureCursorVisible();
        }
        #endregion
    }
}
