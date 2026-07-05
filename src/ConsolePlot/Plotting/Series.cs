using System;
using System.Collections.Generic;
using System.Linq;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A line series: data points connected by lines. The base <see cref="PlotElement"/> for the point-based plot
    /// types (<see cref="ScatterSeries"/>, <see cref="StemSeries"/>), which share its data and pen.
    /// </summary>
    public class Series : PlotElement
    {
        /// <summary>
        /// Gets the X values of the series.
        /// </summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>
        /// Gets the Y values of the series.
        /// </summary>
        public IReadOnlyList<double> Ys { get; }

        /// <summary>
        /// Gets the pen used to draw the series.
        /// </summary>
        public PointPen Pen { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Series"/> class.
        /// </summary>
        /// <param name="xs">The X values of the series.</param>
        /// <param name="ys">The Y values of the series.</param>
        /// <param name="pen">The pen to use for drawing the series.</param>
        /// <exception cref="ArgumentException">Thrown when xs and ys have different lengths or when pen is null.</exception>
        public Series(IEnumerable<double> xs, IEnumerable<double> ys, PointPen pen)
        {
            Xs = xs.ToList();
            Ys = ys.ToList();

            if (Xs.Count != Ys.Count)
                throw new ArgumentException("X and Y collections must have the same length.");
            if (pen.Equals(default(PointPen)))
                throw new ArgumentException("Pen cannot be null.", nameof(pen));
            Pen = pen;
        }

        internal override Bounds GetDataBounds() => Bounds.FromXY(Xs, Ys);

        internal override void Draw(GraphGraphics graphics) => graphics.DrawLines(Pen, Xs, Ys);
    }

    /// <summary>A scatter series: the data points drawn as markers, without connecting lines.</summary>
    public class ScatterSeries : Series
    {
        public ScatterSeries(IEnumerable<double> xs, IEnumerable<double> ys, PointPen pen) : base(xs, ys, pen) { }

        internal override void Draw(GraphGraphics graphics) => graphics.DrawPoints(Pen, Xs, Ys);
    }

    /// <summary>A stem series: a vertical line from <see cref="Baseline"/> to each point, capped with a marker.</summary>
    public class StemSeries : Series
    {
        /// <summary>The value the stems rise from (usually 0 or the x-axis).</summary>
        public double Baseline { get; }

        /// <summary>The pen used to draw the vertical stems.</summary>
        public LinePen StemPen { get; }

        public StemSeries(IEnumerable<double> xs, IEnumerable<double> ys, PointPen pen, double baseline = 0, LinePen stemPen = null)
            : base(xs, ys, pen)
        {
            Baseline = baseline;
            StemPen = stemPen ?? new LinePen(SystemLineBrushes.Thin, Plot.GetNearestConsoleColor(pen.Color));
        }

        // Include the baseline so short stems (data far from the baseline) stay visible.
        internal override Bounds GetDataBounds() => base.GetDataBounds()?.IncludeY(Baseline);

        internal override void Draw(GraphGraphics graphics) => graphics.DrawStems(StemPen, Pen, Xs, Ys, Baseline);
    }
}
