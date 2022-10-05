using System.Collections.Generic;
using QtCore;

namespace ParatextQtPOC
{
    internal static class QtUtils
    {
        public static IEnumerable<string> ToEnumerable(this QStringList list)
        {
            for (int i = 0; i < list.Length; i++)
                yield return list.Value(i);
        }
    }
}
