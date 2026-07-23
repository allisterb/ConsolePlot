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
            _target.Write(x, Height - 1 - y, new Character(c, foregroundColor, backgroundColor));
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
