using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A vertical error-bar series: each point (x, y) is drawn as a whisker from <c>y − errLow</c> to
    /// <c>y + errHigh</c> with horizontal caps and a centre marker. Errors are asymmetric (separate low/high);
    /// pass the same array to both for symmetric bars. Cell resolution, full RGB colour.
    /// </summary>
    public class ErrorBarSeries : PlotElement
    {
        /// <summary>The X positions of the points.</summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>The Y values (centre of each error bar).</summary>
        public IReadOnlyList<double> Ys { get; }

        /// <summary>Downward error magnitudes (whisker extends to <c>y − errLow</c>).</summary>
        public IReadOnlyList<double> ErrLows { get; }

        /// <summary>Upward error magnitudes (whisker extends to <c>y + errHigh</c>).</summary>
        public IReadOnlyList<double> ErrHighs { get; }

        /// <summary>Colour of the whisker, caps and marker.</summary>
        public ConsoleGUI.Data.Color Color { get; }

        /// <summary>Cap half-width in cells (a cap spans <c>2·capRadius + 1</c> cells).</summary>
        public int CapRadius { get; }

        public ErrorBarSeries(
            IEnumerable<double> xs, IEnumerable<double> ys, IEnumerable<double> errLows, IEnumerable<double> errHighs,
            ConsoleGUI.Data.Color color, int capRadius = 1)
        {
            Xs = new List<double>(xs);
            Ys = new List<double>(ys);
            ErrLows = new List<double>(errLows);
            ErrHighs = new List<double>(errHighs);

            int n = Xs.Count;
            if (Ys.Count != n || ErrLows.Count != n || ErrHighs.Count != n)
                throw new ArgumentException("xs, ys, errLows and errHighs must have the same length.");

            Color = color;
            CapRadius = Math.Max(0, capRadius);
        }

        // Y-range spans each point's low and high whisker ends.
        internal override Bounds GetDataBounds()
        {
            int n = Xs.Count;
            var lows = new double[n];
            var highs = new double[n];
            for (int i = 0; i < n; i++)
            {
                lows[i] = Ys[i] - Math.Abs(ErrLows[i]);
                highs[i] = Ys[i] + Math.Abs(ErrHighs[i]);
            }

            return Bounds.Union(Bounds.FromXY(Xs, lows), Bounds.FromXY(Xs, highs));
        }

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawErrorBars(Color, Xs, Ys, ErrLows, ErrHighs, CapRadius);
    }
}
