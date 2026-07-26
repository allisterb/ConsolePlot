using System;
using ConsoleGUI.Api;
using ConsoleGUI.Data;
using ConsoleGUI.Space;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// The cell surface the plot renderer draws into. Backed by an <see cref="IConsoleBuffer"/> render target, so a
    /// host (e.g. the Jumbee <c>Plot</c> control) can have the plot drawn straight into its own buffer with no
    /// intermediate copy. The plot's coordinates are <b>y-up</b> (row 0 at the bottom); the target is top-down, so the
    /// flip (<c>Height - 1 - y</c>) is applied here on every access — the drawing code stays y-up and unchanged.
    /// </summary>
    public readonly struct ConsoleImage
    {
        private readonly IConsoleBuffer _target;

        // Which cells this image has written since the last Clear. A reference field, so every copy of this readonly
        // struct (it is passed by value into the graphics classes) shares the one tracker.
        private readonly DrawnCells _drawn;

        /// <summary>Gets the width of the image.</summary>
        public int Width { get; }

        /// <summary>Gets the height of the image.</summary>
        public int Height { get; }

        /// <summary>Initializes an image backed by a private in-memory buffer (the standalone default).</summary>
        public ConsoleImage(int width, int height) : this(new MemoryConsoleBuffer(width, height)) { }

        /// <summary>Initializes an image that draws straight into <paramref name="target"/>.</summary>
        public ConsoleImage(IConsoleBuffer target)
        {
            _target = target;
            Width = target.Size.Width;
            Height = target.Size.Height;
            _drawn = new DrawnCells(Width * Height);
        }

        /// <summary>The render target this image draws into.</summary>
        internal IConsoleBuffer Target => _target;

        /// <summary>
        /// Sets a pixel at the specified (y-up) coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel (y-up).</param>
        /// <param name="c">The character to set.</param>
        /// <param name="foregroundColor">The foreground color of the pixel.</param>
        /// <param name="backgroundColor">The background color of the pixel, or <see langword="null"/> for transparent.</param>
        public void SetPixel(int x, int y, char c, ConsoleGUI.Data.Color foregroundColor, ConsoleGUI.Data.Color? backgroundColor = null)
        {
            // No bounds check: the render target throws on an out-of-range write, and every caller already clips to
            // the image via ClipBounds, so a guard here would only add a redundant branch to the per-point hot path.
            var row = Height - 1 - y;
            _drawn?.Mark((row * Width) + x);
            _target.Write(x, row, new Character(c, foregroundColor, backgroundColor));
        }

        /// <summary>
        /// Erases the cells drawn since the previous clear, rather than every cell in the image.
        /// </summary>
        /// <remarks>
        /// A plot redraws from scratch each frame, so the old content has to go — but blanking the WHOLE surface
        /// costs one write per cell regardless of how little was drawn, which for a live plot (see the host's
        /// <c>AddLiveSeries</c>) is paid on every tick forever. A sparse figure touches a small fraction of its
        /// cells, so erasing just those turns the per-frame cost from O(area) into O(content).
        /// <para>The first clear after construction (or a resize, which builds a new image) still blanks everything:
        /// the render target may hold content this image never wrote and therefore knows nothing about.</para>
        /// </remarks>
        internal void ClearDrawn(char clearChar, ConsoleGUI.Data.Color clearColor)
        {
            var blank = new Character(clearChar, clearColor);
            if (_drawn is null || _drawn.NeedsFullClear)
            {
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                        _target.Write(x, y, blank);
                _drawn?.Reset();
                return;
            }

            foreach (var index in _drawn.Marked)
                _target.Write(index % Width, index / Width, blank);
            _drawn.Reset();
        }

        /// <summary>
        /// Gets the <see cref="Character"/> at the specified (y-up) coordinates. Returned straight from the render
        /// target with no repack — the drawing code reads <c>Content</c>/<c>Foreground</c> directly. An undrawn or
        /// cleared cell has a <see langword="null"/> <c>Content</c>/<c>Foreground</c> (treat as an empty glyph so the
        /// braille/cross read-back starts fresh).
        /// </summary>
        /// <param name="x">The x-coordinate of the cell.</param>
        /// <param name="y">The y-coordinate of the cell (y-up).</param>
        /// <returns>The character at the specified coordinates.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown by the render target when coordinates are out of bounds.</exception>
        public Character GetCharacter(int x, int y) => _target.CharacterAt(x, Height - 1 - y);
    }
}
