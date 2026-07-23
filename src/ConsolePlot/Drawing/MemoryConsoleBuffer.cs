using ConsoleGUI.Api;
using ConsoleGUI.Data;
using ConsoleGUI.Space;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// A plain in-memory <see cref="IConsoleBuffer"/> — a <see cref="Character"/> grid. The default render target for a
    /// standalone <see cref="ConsolePlot.Plot"/> (one not drawing into a host buffer); the console <c>Render()</c>
    /// reads it back.
    /// </summary>
    internal sealed class MemoryConsoleBuffer : IConsoleBuffer
    {
        private readonly Character[,] _cells;

        public MemoryConsoleBuffer(int width, int height)
        {
            _cells = new Character[height, width];
            Size = new Size(width, height);
        }

        public Size Size { get; }

        public Character CharacterAt(int x, int y) => _cells[y, x];

        public void Write(int x, int y, in Character character) => _cells[y, x] = character;
    }
}
