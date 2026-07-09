using System;
using System.Collections.Generic;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Represents the tick settings for the plot axes.
    /// </summary>
    public class TickSettings
    {
        /// <summary>
        /// Explicit horizontal-axis ticks (value + label). When set, they are used verbatim instead of the
        /// auto-generated numeric ticks, and the data bounds are left unadjusted — for categorical axes such as a
        /// confusion matrix's class names. <see langword="null"/>/empty means auto.
        /// </summary>
        public IReadOnlyList<(double Value, string Label)> CustomXTicks { get; set; }

        /// <summary>Explicit vertical-axis ticks; see <see cref="CustomXTicks"/>.</summary>
        public IReadOnlyList<(double Value, string Label)> CustomYTicks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ticks are visible.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Gets or sets the pen used to draw the ticks.
        /// </summary>
        public LinePen Pen { get; set; } = new LinePen(SystemLineBrushes.Thin, ConsoleColor.White);

        /// <summary>
        /// Gets or sets the desired step between ticks on the horizontal axis.
        /// </summary>
        public int DesiredXStep { get; set; } = 11;

        /// <summary>
        /// Gets or sets the desired step between ticks on the vertical axis.
        /// </summary>
        public int DesiredYStep { get; set; } = 3;

        /// <summary>
        /// Gets the label settings associated with the ticks.
        /// </summary>
        public LabelSettings Labels { get; } = new LabelSettings();

        /// <summary>
        /// Validates the tick settings.
        /// </summary>
        public void Validate()
        {
            Labels.Validate();
        }
    }
}