using System;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Represents the overall plot settings.
    /// </summary>
    public class PlotSettings
    {
        /// <summary>
        /// Gets the axis settings for the plot.
        /// </summary>
        public AxisSettings Axis { get; } = new AxisSettings();

        /// <summary>
        /// Gets the grid settings for the plot.
        /// </summary>
        public GridSettings Grid { get; } = new GridSettings();

        /// <summary>
        /// Gets the tick settings for the plot.
        /// </summary>
        public TickSettings Ticks { get; } = new TickSettings();

        /// <summary>
        /// A fixed (min, max) data range for the horizontal axis, or <see langword="null"/> to auto-scale to the data.
        /// When set, the axis is pinned to this range verbatim (no nice-number bounds adjustment) — so live updates
        /// move only the data, not the axis; data outside the range is clipped.
        /// </summary>
        public (double Min, double Max)? FixedXRange { get; set; }

        /// <summary>A fixed (min, max) data range for the vertical axis; see <see cref="FixedXRange"/>.</summary>
        public (double Min, double Max)? FixedYRange { get; set; }

        /// <summary>
        /// A sliding horizontal window width, or <see langword="null"/> for none. When set, the X axis shows the last
        /// <c>XWindow</c> units of data — <c>[max(0, dataMax − XWindow), dataMax]</c> — so a monotonic (time-like)
        /// series scrolls forward only and never shows x &lt; 0. Ignored if <see cref="FixedXRange"/> is set.
        /// </summary>
        public double? XWindow { get; set; }

        /// <summary>
        /// Gets or sets the default brush used to draw series if none is provided.
        /// </summary>
        public IPointBrush DefaultGraphBrush { get; set; } = SystemPointBrushes.Braille;

        /// <summary>
        /// Validates all settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when any setting is invalid.</exception>
        public void Validate()
        {
            Axis.Validate();
            Grid.Validate();
            Ticks.Validate();
        }
    }
}