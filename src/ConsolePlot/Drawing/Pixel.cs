using System;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// Represents a pixel in the console image.
    /// </summary>
    public readonly struct Pixel
    {
        /// <summary>
        /// Gets the character of the pixel.
        /// </summary>
        public char Character { get; }

        /// <summary>
        /// Gets the foreground color of the pixel.
        /// </summary>
        public ConsoleGUI.Data.Color ForegroundColor { get; }

        /// <summary>
        /// Gets the background color of the pixel, or <see langword="null"/> for a transparent background.
        /// </summary>
        public ConsoleGUI.Data.Color? BackgroundColor { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pixel"/> struct.
        /// </summary>
        /// <param name="character">The character of the pixel.</param>
        /// <param name="foregroundColor">The foreground color of the pixel.</param>
        /// <param name="backgroundColor">The background color of the pixel, or <see langword="null"/> for transparent.</param>
        public Pixel(char character, ConsoleGUI.Data.Color foregroundColor, ConsoleGUI.Data.Color? backgroundColor = null)
        {
            Character = character;
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
        }
    }
}