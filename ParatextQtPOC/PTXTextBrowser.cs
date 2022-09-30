using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    internal class PTXTextBrowser : QTextBrowser
    {
        public PTXTextBrowser(QWidget parent) : base(parent) { }

        enum safeKeys
        {
            keyLeft = 0x01000012,
            keyUp = 0x01000013,
            keyRight = 0x01000014,
            keyDown = 0x01000015
        }

        protected override unsafe void OnKeyPressEvent(QKeyEvent @event)
        {
            if (CurrentCharFormat.HasProperty(TextEdit.READONLY_TEXT_PROPERTY) &&
                !Enum.IsDefined(typeof(safeKeys), @event.Key) )
                return;
            base.OnKeyPressEvent(@event);
        }

        //protected override unsafe void OnKeyReleaseEvent(QKeyEvent @event)
        //{
        //    base.OnKeyReleaseEvent(@event);
        //}
    }
}
