using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paratext.Data;
using PtxUtils;
using QtCore.Qt;
using QtGui;

namespace ParatextQtPOC
{
    public sealed class TextEditUsfmLoad : UsfmParserSink
    {
        #region Member variables
        private readonly ScrText scrText;
        private readonly List<Annotation> currentVerseAnnotations = new List<Annotation>();
        private readonly QTextCharFormat markerFormat = new QTextCharFormat();
        private readonly QTextCharFormat callerFormat = new QTextCharFormat();
        private readonly QTextCharFormat attributeFormat = new QTextCharFormat();
        private readonly QTextCursor cursor;
        private readonly StringBuilder noteBuilder = new StringBuilder();
        private readonly ScrStylesheet styleSheet;
        private readonly Dictionary<string, Annotation> createdAnnotations;
        private readonly IList<AnnotationSource> annotationSources;
        private readonly StyleSheetHelper styleHelper;

        private QTextCharFormat currentParaCharFormat;
        private QTextCharFormat currentCharFormat;
        private int incrementedNoteCount;
        private string currentNoteCaller;
        private bool inNote;
        private bool charIsClosed;
        private bool noteIsClosed;
        #endregion

        #region Constructor
        public TextEditUsfmLoad(ScrText scrText, int bookNum, QTextCursor cursor, IList<AnnotationSource> annotationSources, 
            Dictionary<string, Annotation> createdAnnotations)
        {
            this.scrText = scrText;
            this.cursor = cursor;
            this.annotationSources = annotationSources;
            this.createdAnnotations = createdAnnotations;

            styleSheet = scrText.ScrStylesheet(bookNum);
            styleHelper = StyleSheetHelper.Get(scrText, bookNum);

            markerFormat.Foreground = new QBrush(GlobalColor.DarkGray);
            markerFormat.Font = new QFont("Arial", 14);
            // markerFormat.SetProperty(TextEdit.IGNORE_FRAGMENT_PROPERTY, 1);

            callerFormat.Foreground = new QBrush(GlobalColor.DarkBlue);
            callerFormat.Font = new QFont(styleHelper.DefaultFont.DefaultFamily, styleHelper.DefaultFont.PointSize, (int)QFont.Weight.Bold);
            callerFormat.verticalAlignment = QTextCharFormat.VerticalAlignment.AlignSuperScript;

            attributeFormat.Foreground = new QBrush(GlobalColor.DarkGray);
            attributeFormat.Font = styleHelper.DefaultFont;
        }
        #endregion

        #region Overrides of UsfmParserSink
        public override void StartBook(UsfmParserState state, string marker, string code)
        {
            StartPara(state, marker, false, null);
            cursor.InsertText(code + " ", currentCharFormat);
        }

        public override void Chapter(UsfmParserState state, string number, string marker, string altNumber, string pubNumber)
        {
            StartPara(state, marker, false, null);
            cursor.InsertText(number + " ", currentCharFormat);
        }

        public override void Verse(UsfmParserState state, string number, string marker, string altNumber, string pubNumber)
        {
            StartChar(state, marker, true, false, null);
            cursor.InsertText(number + " ", currentCharFormat);
            currentCharFormat = currentParaCharFormat;

            currentVerseAnnotations.Clear();
            currentVerseAnnotations.AddRange(annotationSources.SelectMany(a => a.GetAnnotations(state.VerseRef)));
        }

        public override void StartPara(UsfmParserState state, string marker, bool unknown, NamedAttribute[] namedAttributes)
        {
            ParagraphStyleInfo styleInfo = styleHelper.GetParaStyle(marker);
            if (styleInfo != null)
            {
                currentParaCharFormat = styleInfo.CharFormat;
                currentCharFormat = currentParaCharFormat;
                if (cursor.AtStart)
                {
                    cursor.BlockFormat = styleInfo.ParaFormat;
                    cursor.BlockCharFormat = styleInfo.CharFormat;
                }
                else
                    cursor.InsertBlock(styleInfo.ParaFormat, styleInfo.CharFormat);
            }

            QTextCharFormat verseId = new QTextCharFormat(markerFormat);
            verseId.SetProperty(TextEdit.VERSE_ID_PROPERTY, state.VerseRef.ToString());

            string beginning = scrText.RightToLeft ? StringUtils.rtlMarker.ToString() : "";
            cursor.InsertText($"{beginning}\\{marker} ", verseId);
        }

        public override void StartChar(UsfmParserState state, string markerWithoutPlus, bool closed, bool unknown,
            NamedAttribute[] namedAttributes)
        {
            charIsClosed = closed;
            if (inNote)
            {
                noteBuilder.Append($"\\{markerWithoutPlus} ");
                return;
            }

            currentCharFormat = GetCharStyleForMarker(markerWithoutPlus);
            QTextCharFormat mergedFormat = new QTextCharFormat(currentCharFormat);
            mergedFormat.Merge(markerFormat);

            if (markerWithoutPlus == "v")
            {
                mergedFormat.SetProperty(TextEdit.SPECIAL_PROPERTY, TextEdit.SPECIAL_VERSE);
                mergedFormat.SetProperty(TextEdit.VERSE_ID_PROPERTY, state.VerseRef.ToString());
            }

            cursor.InsertText($"\\{markerWithoutPlus} ", mergedFormat);
        }

        public override void EndChar(UsfmParserState state, string marker, NamedAttribute[] namedAttributes)
        {
            if (inNote)
            {
                if (charIsClosed)
                    noteBuilder.Append($"\\{marker}*");
                return;
            }

            if (namedAttributes != null)
            {
                ScrTag tag = styleSheet.GetTag(marker);
                string attributeText;
                if (namedAttributes.Length == 1 && namedAttributes[0].Name == tag.DefaultAttribute)
                    attributeText = namedAttributes[0].Value;
                else
                    attributeText = string.Join(" ", namedAttributes.Select(a => a.ToString()));
                
                cursor.InsertText("|" + attributeText, attributeFormat);
            }

            currentCharFormat = currentParaCharFormat;
            if (charIsClosed)
                cursor.InsertText($"\\{marker}*", markerFormat);
        }

        public override void StartNote(UsfmParserState state, string marker, string caller, string category, bool closed)
        {
            inNote = true;
            noteIsClosed = closed;
            noteBuilder.Clear();
            currentNoteCaller = caller;
            noteBuilder.Append($"\\{marker} {caller} ");
        }

        public override void EndNote(UsfmParserState state, string marker)
        {
            inNote = false;
            
            QTextCharFormat noteFormat = new QTextCharFormat(callerFormat);
            noteFormat.SetProperty(TextEdit.SPECIAL_PROPERTY, TextEdit.SPECIAL_FOOTNOTE_CALLER);
            if (noteIsClosed)
                noteBuilder.Append($"\\{marker}*");
            noteFormat.ToolTip = noteBuilder.ToString();

            string callerForView = currentNoteCaller;
            if (currentNoteCaller == "+")
                callerForView = ((char)(incrementedNoteCount++ + 'a')).ToString();
            else if (currentNoteCaller == "-")
                callerForView = "*";

            cursor.InsertText(callerForView, noteFormat);
        }

        public override void Text(UsfmParserState state, string text)
        {
            text = text.Replace('\u00A0', '~'); // For some reason, the UsfmParser replaces the tildes with a nbsp - we need the tildes.
            if (inNote)
            {
                noteBuilder.Append(text);
                return;
            }

            if (currentVerseAnnotations.Count == 0)
            {
                cursor.InsertText(text, currentCharFormat);
                return;
            }

            // Insert annotations into the text
            // This is over-simplified and doesn't handle annotations on verse numbers that aren't exactly
            // the same as in the text and does not handle overlapping annotations.
            int startOffset = state.VerseOffset;
            int endOffset = startOffset + text.Length;

            bool prevWasAnnotation = false;
            foreach (Annotation ann in currentVerseAnnotations
                .Where(a => a.ScriptureSelection.StartPosition >= startOffset && a.ScriptureSelection.StartPosition < endOffset)
                .OrderBy(a => a.ScriptureSelection.StartPosition))
            {
                int textIndex = text.IndexOf(ann.ScriptureSelection.SelectedText, StringComparison.InvariantCultureIgnoreCase);
                if (textIndex == -1)
                    continue;

                if (textIndex > 0)
                {
                    string beforeText = text.Substring(0, textIndex);
                    cursor.InsertText(beforeText, currentCharFormat);
                    prevWasAnnotation = false;
                }

                if (prevWasAnnotation)
                {
                    QTextCharFormat spaceFormat = new QTextCharFormat(currentCharFormat);
                    spaceFormat.SetProperty(TextEdit.IGNORE_FRAGMENT_PROPERTY, true);
                    spaceFormat.SetProperty(TextEdit.READONLY_TEXT_PROPERTY, true);
                    cursor.InsertText(" ", spaceFormat);
                }

                prevWasAnnotation = true;
                string annId = Guid.NewGuid().ToString();
                QTextCharFormat mergedFormat = new QTextCharFormat(currentCharFormat);
                if (ann.TreatSelectedTextAsLink)
                {
                    mergedFormat.Anchor = true;
                    mergedFormat.AnchorHref = "annotation:" + annId;
                }
                if (ann.HoverText != null)
                    mergedFormat.ToolTip = ann.HoverText;
                mergedFormat.Merge(ann.Style);

                if (!string.IsNullOrEmpty(ann.InsertedText))
                {
                    QTextCharFormat insertedTextFormat = new QTextCharFormat(ann.InsertedTextStyle);
                    insertedTextFormat.SetProperty(TextEdit.IGNORE_FRAGMENT_PROPERTY, true);
                    insertedTextFormat.SetProperty(TextEdit.READONLY_TEXT_PROPERTY, true);
                    insertedTextFormat.Anchor = true;
                    insertedTextFormat.AnchorHref = "annotation:" + annId;
                    cursor.InsertText(ann.InsertedText, insertedTextFormat);
                }

                // Ideally we would use QTextImageFormat to insert the image directly, but
                // that seems to require the image be part of the embedded resources - which
                // we couldn't figure out how to add an image to some embedded resources
                // that Qt could access (it may not be possible in QtSharp).
                var imageStyle = string.IsNullOrEmpty(ann.IconStyle) ? "" : $" style='{ann.IconStyle}'";
                cursor.InsertHtml($"<a href='annotationIcon:{annId}'><img src='{ann.IconPath}'{imageStyle}/></a>");
                
                string annotatedText = text.Substring(textIndex, ann.ScriptureSelection.SelectedText.Length);
                cursor.InsertText(annotatedText, mergedFormat);

                createdAnnotations[annId] = ann;

                text = text.Substring(textIndex + annotatedText.Length);
            }

            cursor.InsertText(text, currentCharFormat);
        }
        #endregion

        #region Private helper methods
        private QTextCharFormat GetCharStyleForMarker(string markerWithoutPlus)
        {
            QTextCharFormat charFormat = styleHelper.GetCharStyle(markerWithoutPlus);
            if (charFormat == null)
                return currentParaCharFormat;

            QTextCharFormat returnFormat = new QTextCharFormat(currentParaCharFormat);
            returnFormat.Merge(charFormat);
            return returnFormat;
        }
        #endregion
    }
}
