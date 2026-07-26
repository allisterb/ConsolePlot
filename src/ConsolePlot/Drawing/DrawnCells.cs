using System;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// The set of cells an <see cref="ConsoleImage"/> has written since its last clear, so the next clear can erase
    /// only those instead of the whole surface.
    /// </summary>
    /// <remarks>
    /// Two structures rather than one, on purpose. The <c>bool[]</c> makes "have I already marked this cell?" O(1),
    /// which matters because a cell is commonly written several times in a frame (grid, then an axis, then a series
    /// on top) and the erase list must not grow with repeats. The <c>int[]</c> keeps the marked cells enumerable in
    /// O(marked) — scanning the bool array to find them would put the O(area) cost straight back.
    /// <para>Memory is 5 bytes per cell (1 + 4), i.e. ~36KB for a 240x30 plot, which buys back ~7000 buffer writes
    /// on every frame that plot redraws.</para>
    /// </remarks>
    internal sealed class DrawnCells
    {
        private readonly bool[] _marked;
        private readonly int[] _indices;
        private int _count;

        public DrawnCells(int capacity)
        {
            capacity = Math.Max(0, capacity);
            _marked = new bool[capacity];
            _indices = new int[capacity];
        }

        /// <summary>
        /// True until the first <see cref="Reset"/>. A freshly-built image has no record of what its render target
        /// already contains — for a hosted plot the buffer may still hold the PREVIOUS plot's figure, drawn before a
        /// rebuild or resize replaced the image — so the first clear must blank everything.
        /// </summary>
        public bool NeedsFullClear { get; private set; } = true;

        /// <summary>The cells written since the last <see cref="Reset"/>, in write order.</summary>
        public ReadOnlySpan<int> Marked => new ReadOnlySpan<int>(_indices, 0, _count);

        /// <summary>Records that <paramref name="index"/> (row-major, in render-target coordinates) was written.</summary>
        public void Mark(int index)
        {
            if ((uint)index >= (uint)_marked.Length || _marked[index]) return;
            _marked[index] = true;
            _indices[_count++] = index;
        }

        /// <summary>Forgets every mark, ready for the next frame's writes.</summary>
        public void Reset()
        {
            for (var i = 0; i < _count; i++) _marked[_indices[i]] = false;
            _count = 0;
            NeedsFullClear = false;
        }
    }
}
