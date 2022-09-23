using System;
using System.Collections.Generic;
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
    public class NotesAnnotationSource : AnnotationSource
    {
        private readonly Dictionary<VerseRef, List<CommentThread>> notesOnVerse = new Dictionary<VerseRef, List<CommentThread>>();
        private readonly ScrText scrText;

        public NotesAnnotationSource(ScrText scrText)
        {
            this.scrText = scrText;

            foreach (CommentThread thread in CommentManager.Get(scrText).FindThreads())
            {
                if (!notesOnVerse.TryGetValue(thread.VerseRef, out List<CommentThread> notes))
                    notesOnVerse[thread.VerseRef] = notes = new List<CommentThread>();
                notes.Add(thread);
            }
        }

        /// <summary>
        /// Gets all annotations for the specified reference and text name.
        /// </summary>
        /// <param name="scrText">project</param>
        /// <param name="verseRef">verse reference to find annotations for</param>
        /// <returns>all annotations for the verse. Or null if AnnotationSource Implementation requires usfm.</returns>
        public IEnumerable<Annotation> GetAnnotations(VerseRef verseRef)
        {
            CommentTags tags = CommentTags.Get(scrText);
            if (!notesOnVerse.TryGetValue(verseRef, out List<CommentThread> notes)) 
                yield break;

            foreach (CommentThread thread in notes)
                yield return new NotesAnnotation(thread, tags);
        }

        #region NotesAnnotation class
        private sealed class NotesAnnotation : Annotation
        {
            private static readonly QTextCharFormat style;

            static NotesAnnotation()
            {
                style = new QTextCharFormat();
                style.underlineStyle = QTextCharFormat.UnderlineStyle.DashUnderline;
                style.UnderlineColor = GlobalColor.DarkGray;
            }

            public NotesAnnotation(CommentThread thread, CommentTags tags)
            {
                IconPath = Path.Combine(Environment.CurrentDirectory, "TagIcons", tags.Get(thread.TagIds.First()).Icon + ".png");
                Icon = new QImage(IconPath);
                HoverText = thread.Comments.First().Contents?.InnerText;
                ScriptureSelection = thread.ScriptureSelection;
            }
            
            public ScriptureSelection ScriptureSelection { get; }

            public QTextCharFormat Style => style;

            public bool TreatSelectedTextAsLink => false;

            public QImage Icon { get; }

            public string IconPath { get; }

            public string HoverText { get; }
            
            
            public bool Click(int button, bool onIcon, object control, Coordinates point)
            {
                if (!onIcon)
                    return false;

                QMessageBox.Information(null, "Note clicked", HoverText, QMessageBox.StandardButton.Close);
                return true;
            }
        }
        #endregion
    }
}
