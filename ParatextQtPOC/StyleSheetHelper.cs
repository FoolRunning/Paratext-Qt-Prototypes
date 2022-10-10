using System.Collections.Generic;
using Paratext.Data;
using QtCore.Qt;
using QtGui;
using SIL.Scripture;

namespace ParatextQtPOC
{
    internal class StyleSheetHelper
    {
        #region Member variables
        private static Dictionary<string, StyleSheetHelper> styleSheetCache = new Dictionary<string, StyleSheetHelper>();

        private readonly Dictionary<string, ParagraphStyleInfo> paragraphMarkerFormat = new Dictionary<string, ParagraphStyleInfo>();
        private readonly Dictionary<string, QTextCharFormat> characterMarkerFormat = new Dictionary<string, QTextCharFormat>();
        private readonly ScrText scrText;
        #endregion

        #region Constructor
        private StyleSheetHelper(ScrText scrText, int bookNum)
        {
            this.scrText = scrText;
            DefaultFont = new QFont(scrText.Language.FontName, scrText.Language.FontSize);

            foreach (ScrTag tag in scrText.ScrStylesheet(bookNum).Tags)
            {
                switch (tag.StyleType)
                {
                    case ScrStyleType.scNoteStyle:
                    case ScrStyleType.scCharacterStyle:
                        characterMarkerFormat.Add(tag.Marker, CreateCharacterStyleFromTag(tag));
                        break;
                    
                    case ScrStyleType.scParagraphStyle:
                    {
                        QTextCharFormat charFormat = CreateCharacterStyleFromTag(tag);
                        QTextBlockFormat paraFormat = CreateParagraphStyleFromTag(tag);
                        paragraphMarkerFormat.Add(tag.Marker, new ParagraphStyleInfo(paraFormat, charFormat));
                        break;
                    }
                }
            }
        }
        #endregion

        #region Properties
        public QFont DefaultFont { get; }
        #endregion

        #region Public methods
        public static StyleSheetHelper Get(ScrText scrText, int bookNum)
        {
            string key = scrText.Guid + "_";
            if (Canon.IsCanonical(bookNum))
                key += "default";
            else
                key += "extra_" + bookNum;

            StyleSheetHelper helper;
            lock (styleSheetCache)
            {
                if (!styleSheetCache.TryGetValue(key, out helper))
                    styleSheetCache[key] = helper = new StyleSheetHelper(scrText, bookNum);
            }

            return helper;
        }

        public ParagraphStyleInfo GetParaStyle(string marker)
        {
            return paragraphMarkerFormat.TryGetValue(marker, out ParagraphStyleInfo styleInfo) ? styleInfo : null;
        }

        public QTextCharFormat GetCharStyle(string markerWithoutPlus)
        {
            return characterMarkerFormat.TryGetValue(markerWithoutPlus, out QTextCharFormat charFormat) ? charFormat : null;
        }
        #endregion
        
        #region Private helper methods
        private QTextBlockFormat CreateParagraphStyleFromTag(ScrTag tag)
        {
            QTextBlockFormat paraFormat = new QTextBlockFormat();
            paraFormat.SetProperty(TextEditUsfmLoad.PARAGRAPH_MARKER_PROPERTY, tag.Marker);

            if (tag.RawJustificationType != null)
            {
                switch (tag.JustificationType)
                {
                    case ScrJustificationType.scCenter: paraFormat.Alignment = AlignmentFlag.AlignHCenter; break;
                    case ScrJustificationType.scRight: paraFormat.Alignment = AlignmentFlag.AlignRight; break;
                    case ScrJustificationType.scBoth:  paraFormat.Alignment = AlignmentFlag.AlignJustify; break;
                    default: paraFormat.Alignment = AlignmentFlag.AlignLeft; break;
                }
            }

            if (tag.RawFirstLineIndent != null)
                paraFormat.TextIndent = tag.FirstLineIndent * 40 / 100.0; // 40 is the default indent

            if (tag.RawLeftMargin != null)
            {
                double margin = tag.LeftMargin * 40 / 100.0; // 40 is the default indent
                if (scrText.RightToLeft)
                    paraFormat.RightMargin = margin;
                else
                    paraFormat.LeftMargin = margin;
            }

            if (tag.RawRightMargin != null)
            {
                double margin = tag.RightMargin * 40 / 100.0; // 40 is the default indent
                if (scrText.RightToLeft)
                    paraFormat.LeftMargin = margin;
                else
                    paraFormat.RightMargin = margin;
            }

            if (tag.RawLineSpacing != null)
                paraFormat.SetLineHeight(tag.LineSpacing * 100, (int)QTextBlockFormat.LineHeightTypes.ProportionalHeight);

            if (tag.RawSpaceBefore != null)
                paraFormat.TopMargin = tag.SpaceBefore;

            if (tag.RawSpaceAfter != null)
                paraFormat.BottomMargin = tag.SpaceAfter;

            return paraFormat;
        }

        private QTextCharFormat CreateCharacterStyleFromTag(ScrTag tag)
        {
            QTextCharFormat charFormat = new QTextCharFormat();
            int pointSize = 0;
            QFont.Weight? weight = null;
            bool? italic = tag.RawItalic;
            if (tag.RawBold != null)
                weight = tag.Bold ? QFont.Weight.Bold : QFont.Weight.Normal;
            if (tag.RawFontSize != null)
                pointSize = tag.FontSize * DefaultFont.PointSize / 12; // FontSize is treated as a percentage based on 12 being 100%
            if (weight != null || italic != null || pointSize != 0)
            {
                charFormat.Font = new QFont(DefaultFont.Family, pointSize != 0 ? pointSize : DefaultFont.PointSize,
                    weight != null ? (int)weight.Value : -1, italic ?? false);
            }

            if (tag.RawColor != null)
                charFormat.Foreground = new QBrush(new QColor((uint)tag.Color.ARGB));

            if (tag.RawSuperscript != null)
                charFormat.verticalAlignment = tag.Superscript ? QTextCharFormat.VerticalAlignment.AlignSuperScript : QTextCharFormat.VerticalAlignment.AlignNormal;
            else if (tag.RawSubscript != null)
                charFormat.verticalAlignment = tag.Subscript ? QTextCharFormat.VerticalAlignment.AlignSubScript : QTextCharFormat.VerticalAlignment.AlignNormal;

            if (tag.RawSmallCaps != null)
                charFormat.FontCapitalization = tag.SmallCaps ? QFont.Capitalization.SmallCaps : QFont.Capitalization.MixedCase;
    
            return charFormat;
        }
        #endregion
    }

    #region ParagraphStyleInfo class
    internal sealed class ParagraphStyleInfo
    {
        public readonly QTextBlockFormat ParaFormat;
        public readonly QTextCharFormat CharFormat;

        public ParagraphStyleInfo(QTextBlockFormat paraFormat, QTextCharFormat charFormat)
        {
            ParaFormat = paraFormat;
            CharFormat = charFormat;
        }
    }
    #endregion
}
