using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A box-and-whisker series: each entry is drawn as a box from the first quartile (Q1) to the third (Q3) with a
    /// median line, and whiskers extending to the minimum and maximum with caps. Box-drawing glyphs at cell
    /// resolution. Callers supply the five-number summary per box (compute quartiles upstream).
    /// </summary>
    public class BoxSeries : PlotElement
    {
        /// <summary>The X positions of the boxes.</summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>Minimum values (bottom whisker end).</summary>
        public IReadOnlyList<double> Mins { get; }

        /// <summary>First-quartile values (box bottom).</summary>
        public IReadOnlyList<double> Q1s { get; }

        /// <summary>Median values (line inside the box).</summary>
        public IReadOnlyList<double> Medians { get; }

        /// <summary>Third-quartile values (box top).</summary>
        public IReadOnlyList<double> Q3s { get; }

        /// <summary>Maximum values (top whisker end).</summary>
        public IReadOnlyList<double> Maxes { get; }

        /// <summary>Colour of the box, whiskers and caps.</summary>
        public ConsoleGUI.Data.Color BoxColor { get; }

        /// <summary>Colour of the median line.</summary>
        public ConsoleGUI.Data.Color MedianColor { get; }

        /// <summary>Box width as a fraction (0..1) of the spacing between boxes.</summary>
        public double WidthFraction { get; }

        public BoxSeries(
            IEnumerable<double> xs, IEnumerable<double> mins, IEnumerable<double> q1s,
            IEnumerable<double> medians, IEnumerable<double> q3s, IEnumerable<double> maxes,
            ConsoleGUI.Data.Color boxColor, ConsoleGUI.Data.Color medianColor, double widthFraction = 0.6)
        {
            Xs = AsList(xs);
            Mins = AsList(mins);
            Q1s = AsList(q1s);
            Medians = AsList(medians);
            Q3s = AsList(q3s);
            Maxes = AsList(maxes);

            int n = Xs.Count;
            if (Mins.Count != n || Q1s.Count != n || Medians.Count != n || Q3s.Count != n || Maxes.Count != n)
                throw new ArgumentException("All box-summary collections must have the same length as xs.");

            BoxColor = boxColor;
            MedianColor = medianColor;
            WidthFraction = widthFraction;
        }

        // X-range from the positions, y-range spanning mins..maxes.
        internal override Bounds? GetDataBounds() => Bounds.Union(Bounds.FromXY(Xs, Maxes), Bounds.FromXY(Xs, Mins));

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawBoxes(BoxColor, MedianColor, Xs, Mins, Q1s, Medians, Q3s, Maxes, WidthFraction);
    }
}
