using System;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// Represents an image that can be drawn on the console.
    /// </summary>
    public readonly struct ConsoleImage
    {
        public readonly Pixel[,] buffer;

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleImage"/> class with the specified width and height.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        public ConsoleImage(int width, int height)
        {
            Width = width;
            Height = height;
            buffer = new Pixel[height, width];
        }

        /// <summary>
        /// Sets a pixel at the specified coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel.</param>
        /// <param name="c">The character to set.</param>
        /// <param name="foregroundColor">The foreground color of the pixel.</param>
        /// <param name="backgroundColor">The background color of the pixel, or <see langword="null"/> for transparent.</param>
        public void SetPixel(int x, int y, char c, ConsoleGUI.Data.Color foregroundColor, ConsoleGUI.Data.Color? backgroundColor = null)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                buffer[y, x] = new Pixel(c, foregroundColor, backgroundColor);
            }
        }

        /// <summary>
        /// Gets the pixel at the specified coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel.</param>
        /// <returns>The pixel at the specified coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of bounds.</exception>
        public Pixel GetPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates are out of bounds.");
            }
            return buffer[y, x];
        }        
    }
}