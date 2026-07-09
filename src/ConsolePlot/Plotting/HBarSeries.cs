using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A horizontal bar series: each category sits at a Y position and its bar grows along the X axis from
    /// <see cref="Baseline"/> to the value, with a left-anchored eighth-block for the fractional right cell.
    /// </summary>
    public class HBarSeries : PlotElement
    {
        /// <summary>The Y positions of the bars (one per category).</summary>
        public IReadOnlyList<double> Ys { get; }

        /// <summary>The bar values (extent along X).</summary>
        public IReadOnlyList<double> Values { get; }

        /// <summary>The bar colour.</summary>
        public ConsoleGUI.Data.Color Color { get; }

        /// <summary>The value the bars grow from.</summary>
        public double Baseline { get; }

        /// <summary>Bar thickness as a fraction (0..1) of the spacing between bars.</summary>
        public double WidthFraction { get; }

        public HBarSeries(
            IEnumerable<double> ys, IEnumerable<double> values, ConsoleGUI.Data.Color color,
            double baseline = 0, double widthFraction = 0.8)
        {
            Ys = new List<double>(ys);
            Values = new List<double>(values);
            if (Values.Count != Ys.Count)
                throw new ArgumentException("ys and values must have the same length.");

            Color = color;
            Baseline = baseline;
            WidthFraction = widthFraction;
        }

        // Positions are on Y, values on X; include the baseline in the X range so the bars start at it.
        internal override Bounds GetDataBounds() => Bounds.FromXY(Values, Ys)?.IncludeX(Baseline);

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawHBars(Color, Ys, Values, Baseline, WidthFraction);
    }
}
