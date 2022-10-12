using System;
using QtCore;
using QtGui;
using SIL.Scripture;

namespace ParatextQtPOC
{
    internal static class QtHelpers
    {
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Use only if you need to do something with the resulting blocks after iterating.
        /// Otherwise, use <see cref="IterateBlocks"/>.</remarks>
        public static void IterateSafeBlocks(this QTextDocument document, Func<SafeTextBlock, bool> handleBlock)
        {
            QTextBlock currentBlock = document.Begin();
            QTextBlock endBlock = document.End();

            while (currentBlock != endBlock)
            {
                if (!handleBlock(new SafeTextBlock(currentBlock)))
                    break;

                currentBlock = currentBlock.Next;
            }

            endBlock.Dispose();
        }

        public static void IterateBlocks(this QTextDocument document, Func<QTextBlock, bool> handleBlock)
        {
            QTextBlock currentBlock = document.Begin();
            QTextBlock endBlock = document.End();

            while (currentBlock != endBlock)
            {
                using (currentBlock)
                {
                    if (!handleBlock(currentBlock))
                        break;

                    currentBlock = currentBlock.Next;
                }
            }

            endBlock.Dispose();
        }

        public static void IterateFragments(this QTextBlock block, Func<QTextFragment, bool> handleFragment)
        {
            QTextBlock.Iterator iter;
            for (iter = block.Begin(); !iter.AtEnd; iter++)
            {
                using QTextFragment fragment = iter.Fragment;
                if (!handleFragment(fragment))
                    break;
            }

            iter.Dispose();
        }
    }

    #region SafeTextBlock class
    public sealed class SafeTextBlock : IDisposable
    {
        private readonly QTextBlock block;

        public SafeTextBlock(QTextBlock block)
        {
            this.block = block;
        }

        public int Position => block.Position;

        public int Length => block.Length;

        ~SafeTextBlock()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            block?.Dispose(disposing);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static implicit operator QTextBlock(SafeTextBlock block) => block.block;
    }
    #endregion
}
