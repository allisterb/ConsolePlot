using System;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Represents the axis settings for the plot.
    /// </summary>
    public class AxisSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the axis is visible.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Gets or sets the pen used to draw the axis.
        /// </summary>
        public LinePen Pen { get; set; } = new LinePen(SystemLineBrushes.Thin, ConsoleColor.White);

        /// <summary>
        /// Optional caption drawn at the right end of the horizontal axis (the plot's bottom-right corner). It is
        /// screen-anchored — pinned to the image edge, so it does not move when the data range changes. Null or empty
        /// draws nothing.
        /// </summary>
        public string XTitle { get; set; }

        /// <summary>
        /// Optional caption drawn at the top of the vertical axis (the plot's top-left corner). Screen-anchored like
        /// <see cref="XTitle"/>. Null or empty draws nothing.
        /// </summary>
        public string YTitle { get; set; }

        /// <summary>
        /// Colour of the <see cref="XTitle"/>/<see cref="YTitle"/> captions.
        /// </summary>
        public ConsoleColor TitleColor { get; set; } = ConsoleColor.Gray;

        /// <summary>
        /// Validates the axis settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the pen is null.</exception>
        public void Validate()
        {
            if (Pen == null)
                throw new InvalidOperationException("Axis pen cannot be null.");
        }
    }
}