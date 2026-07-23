using System;

namespace ConsolePlot.Drawing.Tools
{
    /// <summary>
    /// Represents a pen for drawing lines with a specific brush and color.
    /// </summary>
    public class LinePen
    {
        /// <summary>
        /// Gets the brush used by this pen.
        /// </summary>
        public LineBrush Brush { get; }

        /// <summary>
        /// Gets the full-RGB color of this pen.
        /// </summary>
        public ConsoleGUI.Data.Color Color { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinePen"/> class with a full-RGB colour.
        /// </summary>
        /// <param name="brush">The brush to use for drawing lines.</param>
        /// <param name="color">The RGB colour of the pen.</param>
        public LinePen(LineBrush brush, ConsoleGUI.Data.Color color)
        {
            Brush = brush;
            Color = color;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinePen"/> class from a 16-colour <see cref="ConsoleColor"/>
        /// (widened to its RGB value). Prefer the <see cref="ConsoleGUI.Data.Color"/> overload for true colour.
        /// </summary>
        /// <param name="brush">The brush to use for drawing lines.</param>
        /// <param name="color">The console colour of the pen.</param>
        public LinePen(LineBrush brush, ConsoleColor color)
        {
            Brush = brush;
            Color = color;
        }
    }
}