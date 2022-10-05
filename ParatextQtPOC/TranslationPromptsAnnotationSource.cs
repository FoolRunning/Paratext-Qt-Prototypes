using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Paratext.Data;
using Paratext.Data.ProjectComments;
using PtxUtils;
using QtCore.Qt;
using QtGui;
using QtWidgets;
using SIL.Scripture;

namespace ParatextQtPOC
{
    public class TranslationPromptsAnnotationSource : AnnotationSource
    {

        private readonly ScrText scrText;
        private readonly List<TranslationPromptAnnotation> translationPromptAnnotations = new List<TranslationPromptAnnotation>();

        public TranslationPromptsAnnotationSource(ScrText scrText)
        {
            this.scrText = scrText;
            CreateTestData();
        }

        /// <summary>
        /// Gets all annotations for the specified reference and text name.
        /// </summary>
        /// <param name="verseRef">verse reference to find annotations for</param>
        /// <returns>all annotations for the verse. Or null if AnnotationSource Implementation requires usfm.</returns>
        public IEnumerable<Annotation> GetAnnotations(VerseRef verseRef)
        {
            return translationPromptAnnotations.Where(a => a.ScriptureSelection.VerseRef.BBBCCCVVV == verseRef.BBBCCCVVV);
        }

        public event EventHandler<AnnotationsChangedEventArgs> AnnotationsChanged;

        private void TogglePrompt(TranslationPromptAnnotation annotation)
        {
            annotation.IsChecked = !annotation.IsChecked;
            
            AnnotationsChanged?.Invoke(this, new AnnotationsChangedEventArgs(scrText, annotation.ScriptureSelection.VerseRef, false));
        }

        private void CreateTestData()
        {
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("PSA"), 1, 1, scrText.Settings.Versification), "Start letter", false));
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("PSA"), 1, 1, scrText.Settings.Versification), "3 things", false));
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("PSA"), 1, 2, scrText.Settings.Versification), "Prayer/request", false));
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("JUD"), 1, 1, scrText.Settings.Versification), "Start letter", false));
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("JUD"), 1, 1, scrText.Settings.Versification), "3 things", false));
            translationPromptAnnotations.Add(new TranslationPromptAnnotation(this, new VerseRef(Canon.BookIdToNumber("JUD"), 1, 2, scrText.Settings.Versification), "Prayer/request", false));
        }
        
        #region TranslationPromptAnnotation class
        private sealed class TranslationPromptAnnotation : Annotation
        {
            private static readonly QTextCharFormat insertedTextStyle;
            private static readonly QTextCharFormat emptyStyle = new QTextCharFormat();

            private readonly TranslationPromptsAnnotationSource owner;
            private string insertedText;
            private bool isChecked;

            static TranslationPromptAnnotation()
            {
                insertedTextStyle = new QTextCharFormat();
                insertedTextStyle.Background = QColor.FromRgba((uint)Color.LightBlue.ToArgb());
            }

            public TranslationPromptAnnotation(TranslationPromptsAnnotationSource owner, VerseRef verse, string insertedText, bool isChecked)
            {
                this.owner = owner;
                this.insertedText = insertedText;
                IsChecked = isChecked;
                ScriptureSelection = new ScriptureSelection(verse, null, verse.Verse.Length + 4);
            }

            public ScriptureSelection ScriptureSelection { get; }

            public QTextCharFormat Style => emptyStyle;

            public bool TreatSelectedTextAsLink => false;

            public string IconPath { get; private set; }

            public string IconStyle => "background-color:LightBlue";

            public string HoverText => "Click me!";

            public bool Click(int button, bool onIcon, object control, Coordinates point)
            {
                if (onIcon)
                {
                    owner.TogglePrompt(this);
                    return true;
                }

                QMessageBox.Information(null, "Translation prompt clicked", "Need to decide what should be done on a text click", QMessageBox.StandardButton.Close);
                return true;
            }

            public string InsertedText => insertedText;

            public QTextCharFormat InsertedTextStyle => insertedTextStyle;

            internal bool IsChecked
            {
                get => isChecked;
                set
                {
                    isChecked = value;
                    IconPath = isChecked ? "resources\\cbox_filled.png" : "resources\\cbox_empty.png";
                }
            }
        }
        #endregion
    }
}
