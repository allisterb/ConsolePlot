using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A vertical bar series: each point is drawn as a filled bar from <see cref="Baseline"/> to its value, with
    /// sub-cell precision at the bar top via eighth-block glyphs. Bars are full-cell colour fills (foreground), so
    /// they use a colour rather than a point brush.
    /// </summary>
    public class BarSeries : PlotElement
    {
        /// <summary>The X positions of the bars.</summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>The bar heights (measured from <see cref="Baseline"/>).</summary>
        public IReadOnlyList<double> Ys { get; }

        /// <summary>The bar fill colour.</summary>
        public ConsoleGUI.Data.Color Color { get; }

        /// <summary>The value the bars rise from (usually 0 or the x-axis).</summary>
        public double Baseline { get; }

        /// <summary>Bar width as a fraction (0..1) of the spacing between adjacent bars.</summary>
        public double WidthFraction { get; }

        public BarSeries(IEnumerable<double> xs, IEnumerable<double> ys, ConsoleGUI.Data.Color color, double baseline = 0, double widthFraction = 0.8)
        {
            Xs = AsList(xs);
            Ys = AsList(ys);
            if (Xs.Count != Ys.Count)
                throw new ArgumentException("X and Y collections must have the same length.");
            Color = color;
            Baseline = baseline;
            WidthFraction = widthFraction;
        }

        // Bars are anchored at the baseline, so it must be inside the y-range for them to render sensibly.
        internal override Bounds? GetDataBounds() => Bounds.FromXY(Xs, Ys)?.IncludeY(Baseline);

        internal override void Draw(GraphGraphics graphics) => graphics.DrawBars(Color, Xs, Ys, Baseline, WidthFraction);
    }
}
