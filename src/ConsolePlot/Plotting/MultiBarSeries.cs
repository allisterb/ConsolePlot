using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A multi-series vertical bar element: several value-series share each x position, drawn either grouped
    /// (side-by-side sub-bars) or stacked (stacked from the baseline). Each series has its own colour.
    /// </summary>
    public class MultiBarSeries : PlotElement
    {
        /// <summary>The X positions of the bar groups.</summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>One value list per series (each the same length as <see cref="Xs"/>).</summary>
        public IReadOnlyList<IReadOnlyList<double>> SeriesValues { get; }

        /// <summary>One colour per series.</summary>
        public IReadOnlyList<ConsoleGUI.Data.Color> Colors { get; }

        /// <summary>The value the bars grow from.</summary>
        public double Baseline { get; }

        /// <summary>Group width as a fraction (0..1) of the spacing between groups.</summary>
        public double WidthFraction { get; }

        /// <summary><see langword="true"/> to stack the series, <see langword="false"/> to group them side by side.</summary>
        public bool Stacked { get; }

        public MultiBarSeries(
            IEnumerable<double> xs, IEnumerable<IEnumerable<double>> seriesValues, IEnumerable<ConsoleGUI.Data.Color> colors,
            bool stacked, double baseline = 0, double widthFraction = 0.8)
        {
            Xs = new List<double>(xs);
            var series = new List<IReadOnlyList<double>>();
            foreach (var s in seriesValues) series.Add(new List<double>(s));
            SeriesValues = series;
            Colors = new List<ConsoleGUI.Data.Color>(colors);
            Stacked = stacked;
            Baseline = baseline;
            WidthFraction = widthFraction;

            if (Colors.Count < SeriesValues.Count)
                throw new ArgumentException("Need at least one colour per series.");
            foreach (var s in SeriesValues)
                if (s.Count != Xs.Count)
                    throw new ArgumentException("Every series must have the same length as xs.");
        }

        internal override Bounds GetDataBounds()
        {
            Bounds bounds = null;
            if (Stacked)
            {
                // The stack track per x reaches from the baseline through each cumulative sum; the axis must span
                // the lowest and highest points the stack reaches.
                var lows = new double[Xs.Count];
                var highs = new double[Xs.Count];
                for (int i = 0; i < Xs.Count; i++)
                {
                    double cumulative = Baseline, lo = Baseline, hi = Baseline;
                    foreach (var s in SeriesValues)
                    {
                        double v = s[i];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        cumulative += v;
                        if (cumulative < lo) lo = cumulative;
                        if (cumulative > hi) hi = cumulative;
                    }
                    lows[i] = lo;
                    highs[i] = hi;
                }
                bounds = Bounds.Union(Bounds.FromXY(Xs, lows), Bounds.FromXY(Xs, highs));
            }
            else
            {
                foreach (var s in SeriesValues)
                    bounds = Bounds.Union(bounds, Bounds.FromXY(Xs, s));
            }

            return bounds?.IncludeY(Baseline);
        }

        internal override void Draw(GraphGraphics graphics)
        {
            if (Stacked)
                graphics.DrawStackedBars(Colors, Xs, SeriesValues, Baseline, WidthFraction);
            else
                graphics.DrawGroupedBars(Colors, Xs, SeriesValues, Baseline, WidthFraction);
        }
    }
}
