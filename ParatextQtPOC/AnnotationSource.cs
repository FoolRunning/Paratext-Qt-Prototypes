using System;
using System.Collections.Generic;
using System.Text;
using Paratext.Data;
using PtxUtils;
using QtGui;
using SIL.Scripture;

namespace ParatextQtPOC
{
    public interface AnnotationSource
    {
        /// <summary>
        /// Gets all annotations for the specified reference and text name.
        /// </summary>
        /// <param name="verseRef">verse reference to find annotations for</param>
        /// <returns>all annotations for the verse. Or null if AnnotationSource Implementation requires usfm.</returns>
        IEnumerable<Annotation> GetAnnotations(VerseRef verseRef);
    }

    public interface Annotation
    {
        /// <summary>
        /// Selection on which the annotation is attached. Note: Annotations may not cross verse boundaries.
        /// </summary>
        ScriptureSelection ScriptureSelection { get; }

        ///// <summary>
        ///// Selection recalculated based on current text and kept for later use, currently only functional in Comment Annotations
        ///// </summary>
        //ScriptureSelection RecalculatedScriptureSelection { set; get; }
		
        /// <summary>
        /// Style of the annotation (e.g. error, warning, keyword, comment)
        /// </summary>
        QTextCharFormat Style { get; }

        bool TreatSelectedTextAsLink { get; }

        /// <summary>
        /// Null for none.
        /// </summary>
        QImage Icon { get; }

        string IconPath { get; }
        
        /// <summary>
        /// Called when a click is performed on an annotation
        /// </summary>
        /// <param name="button">button number (1=left)</param>
        /// <param name="onIcon">true if click was on icon, not text</param>
        /// <param name="control">html editor control</param>
        /// <param name="point">location on control of click</param>
        /// <returns>true if click was handled, false otherwise</returns>
        bool Click(int button, bool onIcon, object control, Coordinates point);
		
        string HoverText { get; }
    }
}
