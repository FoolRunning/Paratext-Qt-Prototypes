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
    #region ReferenceChangedArgs class
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
    #endregion

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
        private QTextDocument workDocument;
        #endregion

        #region Constructor
        public TextForm(ScrText scrText, QWidget parent) : base("Temp", parent)
        {
            TitleBarWidget = new TitleBar(this, "Temp");
            FocusPolicy = FocusPolicy.StrongFocus;

            textBrowser = new PTXTextBrowser(null);
            textBrowser.HorizontalScrollBarPolicy = ScrollBarPolicy.ScrollBarAlwaysOff;
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
                VerseRef newVerseRef = value;
                newVerseRef.ChangeVersification(scrText.Settings.Versification);

                if (currentBook != newVerseRef.BookNum)
                    LoadBook(newVerseRef.BookNum);

                ScrollVerseIntoView(newVerseRef);
                UpdateWindowTitle();
            }
        }

        public event EventHandler<ReferenceChangedArgs> ReferenceChanged;
        #endregion

        #region Public methods
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
        #endregion

        #region Overrides of QDockWidget
        protected override void OnFocusInEvent(QFocusEvent @event)
        {
            base.OnFocusInEvent(@event);
            textBrowser.SetFocus(FocusReason.OtherFocusReason);
        }
        #endregion

        #region Event handlers
        private void Thread_Finished()
        {
            LoadBookComplete(currentBook);
        }

        private void TextBrowser_CursorPositionChanged()
        {
            if (loadingText)
                return;

            VerseRef newReference = CurrentReference;
            if (lastReference.IsDefault || !lastReference.Equals(newReference))
            {
                lastReference = newReference;
                UpdateWindowTitle();
                ReferenceChanged?.Invoke(this, new ReferenceChangedArgs(lastReference, newReference));
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
                LoadBook(currentBook);
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

        private void LoadBook(int bookNum)
        {
            if (bookNum < 1 || bookNum > Canon.LastBook)
                throw new ArgumentException($"bookNum ({bookNum}) is out of range");

            if (loadingText)
            {
                Debug.WriteLine($"Re-entrant call to load a book! Current:{currentBook}, New:{bookNum}");
                return;
            }

            loadingText = true;
            textBrowser.Enabled = true;
            currentBook = bookNum;

            StyleSheetHelper.Get(scrText, bookNum); // Load cache on the main thread.

            var thread = new LoadingThread(this, bookNum);
            thread.Finished += Thread_Finished;
            thread.Start();
        }

        private void LoadBookAsync(int bookNum)
        {
            if (bookNum <= 0)
                return;

            Stopwatch sw = Stopwatch.StartNew();

            QTextDocument document = new QTextDocument();
            document.DefaultTextOption.TextDirection = scrText.RightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
            QTextCursor cursor = new QTextCursor(document);
            Debug.Assert(cursor.AtStart);
            List<UsfmToken> tokens = scrText.Parser.GetUsfmTokens(bookNum);
            FormatText(bookNum, tokens, cursor);
            document.MoveToThread(QCoreApplication.Instance.Thread);

            sw.Stop();
            Debug.WriteLine($"Formatting {Canon.BookNumberToId(bookNum)} from {scrText.Name} took {sw.ElapsedMilliseconds}ms");
            workDocument = document;
        }

        private void LoadBookComplete(int bookNum)
        {
            Stopwatch sw = Stopwatch.StartNew();

            loadingText = false;
            Debug.Assert(QThread.CurrentThread == QCoreApplication.Instance.Thread, "This was not called on the main thread (for some reason)");
            Debug.Assert(workDocument.Thread == QThread.CurrentThread, "Failed to move document to main thread (for some reason): " + scrText.Name);

            textBrowser.CursorPositionChanged -= TextBrowser_CursorPositionChanged;

            QTextDocument prevDocument = textBrowser.Document;
            textBrowser.Document = workDocument;
            workDocument = null;
            prevDocument?.Dispose();

            textBrowser.CursorPositionChanged += TextBrowser_CursorPositionChanged;

            sw.Stop();
            Debug.WriteLine($"Updating browser copy of {Canon.BookNumberToId(bookNum)} from {scrText.Name} took {sw.ElapsedMilliseconds}ms");
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
            FindBlocksForReference(reference, out SafeTextBlock startBlock, out SafeTextBlock endBlock, out string usfm);
            if (startBlock == null || endBlock == null)
                return; // reference was not in the document

            VerseRef startRef = ReferenceFromBlock(startBlock, startBlock.Position);

            // TODO: Remove annotations from annotationsInView that will get deleted
            //VerseRef endRef = ReferenceFromBlock(endBlock, endBlock.Position + endBlock.Length);

            int previousPosition = textBrowser.TextCursor.Position;
            QTextCursor cursor = new QTextCursor(startBlock);
            cursor.BeginEditBlock();

            // Select the blocks and delete them from the view
            cursor.MovePosition(QTextCursor.MoveOperation.Right, QTextCursor.MoveMode.KeepAnchor,
                endBlock.Position + endBlock.Length - startBlock.Position);
            cursor.RemoveSelectedText();
            Debug.Assert(cursor.Anchor == cursor.SelectionEnd);

            // Need to move to the end of the previous block to start inserting new blocks.
            // If we don't do that, the new blocks get added to the beginning of the current block
            // which has a side effect of creating a block that has nothing in it.
            cursor.MovePosition(QTextCursor.MoveOperation.PreviousBlock);
            cursor.MovePosition(QTextCursor.MoveOperation.EndOfBlock);

            // Generate the text formatting for the USFM that was in the deleted blocks. This will
            // insert the new blocks where the cursor is currently located.
            List<UsfmToken> tokens = UsfmToken.Tokenize(scrText, startRef.BookNum, usfm);
            UsfmParser parser = new UsfmParser(scrText.ScrStylesheet(startRef.BookNum), tokens,
                startRef, new TextEditUsfmLoad(scrText, startRef.BookNum, cursor, annotationSources, annotationsInView));
            parser.ProcessTokens();
            
            cursor.EndEditBlock();

            // Put the cursor back where it was before the reset
            QTextCursor newCursor = new QTextCursor(textBrowser.Document);
            newCursor.SetPosition(previousPosition);
            textBrowser.TextCursor = newCursor;
        }

        private void FindBlocksForReference(VerseRef reference,
            out SafeTextBlock startBlock, out SafeTextBlock endBlock,
            out string containedUsfm)
        {
            StringBuilder usfm = new StringBuilder();
            SafeTextBlock theStartBlock = null;
            SafeTextBlock theEndBlock = null;
            SafeTextBlock previousBlock = null;
            textBrowser.Document.IterateSafeBlocks(block =>
            {
                VerseRef blockStartRef = ReferenceFromBlock(block, block.Position);
                SafeTextBlock blockToUse = previousBlock ?? block;
                if (theStartBlock == null && blockStartRef.Equals(reference))
                {
                    theStartBlock = blockToUse;
                    usfm.AppendLine(UsfmForBlock(blockToUse));
                }

                if (blockStartRef > reference)
                {
                    if (theStartBlock == null) // Reference is likely in the middle of a single block
                    {
                        theStartBlock = blockToUse;
                        usfm.AppendLine(UsfmForBlock(theStartBlock));
                    }

                    theEndBlock = blockToUse;
                    return false;
                }

                if (theStartBlock != null)
                    usfm.AppendLine(UsfmForBlock(block));

                previousBlock = block;
                return true;
            });

            startBlock = theStartBlock;
            endBlock = theEndBlock;
            
            if (endBlock == null && startBlock != null) // Can happen if we hit the end of the document
                endBlock = previousBlock;

            Debug.Assert((startBlock == null && endBlock == null) || (startBlock != null && endBlock != null), "Not able to find consistent blocks");
            containedUsfm = usfm.ToString();
        }

        private static string UsfmForBlock(QTextBlock block)
        {
            StringBuilder strBldr = new StringBuilder();
            block.IterateFragments(fragment =>
            {
                string text = fragment.Text; // TODO: Currently this is a memory leak we can't do anything about!

                using QTextCharFormat charFormat = fragment.CharFormat;
                if (charFormat.HasProperty(TextEditUsfmLoad.SPECIAL_PROPERTY))
                {
                    switch (charFormat.property(TextEditUsfmLoad.SPECIAL_PROPERTY).ToInt())
                    {
                        case TextEditUsfmLoad.SPECIAL_VERSE:
                            strBldr.AppendLine();
                            strBldr.Append(text);
                            break;
                        case TextEditUsfmLoad.SPECIAL_FOOTNOTE_CALLER:
                            strBldr.Append(charFormat.ToolTip);
                            break;
                    }
                }
                else if (!charFormat.HasProperty(TextEditUsfmLoad.IGNORE_FRAGMENT_PROPERTY) && (text.Length > 1 || !text.StartsWith(StringUtils.orcCharacter)))
                    strBldr.Append(text);

                return true;
            });

            return strBldr.ToString();
        }
        
        private VerseRef ReferenceFromBlock(QTextBlock block, int position)
        {
            string lastBlockReference = null;
            block.IterateFragments(fragment =>
            {
                if (fragment.Position > position)
                    return false;

                using QTextCharFormat charFormat = fragment.CharFormat;
                if (charFormat.HasProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY))
                    lastBlockReference = charFormat.property(TextEditUsfmLoad.VERSE_ID_PROPERTY).ToString();
                return true;
            });

            return lastBlockReference != null ? new VerseRef(lastBlockReference, scrText.Settings.Versification) : new VerseRef();
        }

        private void ScrollVerseIntoView(VerseRef verseRef)
        {
            Debug.Assert(verseRef.Versification == scrText.Settings.Versification);

            Stopwatch sw = Stopwatch.StartNew();

            int verseFragmentOffset = -1;
            textBrowser.Document.IterateBlocks(block =>
            {
                block.IterateFragments(fragment =>
                {
                    using QTextCharFormat charFormat = fragment.CharFormat;
                    if (!charFormat.HasProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY))
                        return true;

                    VerseRef fragmentVerseRef = new VerseRef(charFormat.StringProperty(TextEditUsfmLoad.VERSE_ID_PROPERTY), scrText.Settings.Versification);
                    if (!verseRef.OverlapsAny(fragmentVerseRef))
                        return true;

                    verseFragmentOffset = fragment.Position + fragment.Length;
                    return false;
                });

                return verseFragmentOffset == -1;
            });

            if (verseFragmentOffset == -1)
                return;

            textBrowser.CursorPositionChanged -= TextBrowser_CursorPositionChanged;

            QTextCursor cursor = new QTextCursor(textBrowser.Document);
            cursor.SetPosition(verseFragmentOffset + verseRef.Verse.Length + 1);
            textBrowser.TextCursor = cursor;
            textBrowser.EnsureCursorVisible();

            textBrowser.CursorPositionChanged += TextBrowser_CursorPositionChanged;

            sw.Stop();
            Debug.WriteLine($"Scrolling verse into view for {scrText.Name} took {sw.ElapsedMilliseconds}ms");
        }
        #endregion

        #region LoadingThread class
        private sealed class LoadingThread : QThread
        {
            private readonly TextForm window;
            private readonly int bookNum;

            internal LoadingThread(TextForm window, int bookNum)
            {
                this.window = window;
                this.bookNum = bookNum;
            }

            protected override void Run()
            {
                window.LoadBookAsync(bookNum);
            }
        }
        #endregion
    }
}
