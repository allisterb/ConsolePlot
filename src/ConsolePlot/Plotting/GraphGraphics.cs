using System;
using System.Collections.Generic;
using ConsolePlot.Drawing;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Provides drawing capabilities for graphs using double-precision coordinates.
    /// </summary>
    internal class GraphGraphics
    {
        private readonly ConsoleGraphics _graphics;
        private readonly CoordinateConverter _converter;

        public GraphGraphics(ConsoleGraphics graphics, CoordinateConverter converter)
        {
            _graphics = graphics;
            _converter = converter;
        }

        public void DrawLines(PointPen pen, IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            var graphics = new VirtualGraphics(_graphics.GetImage(), pen);
            var converter = ScaledConverter(pen);

            int? x1 = null, y1 = null;

            for (int i = 0; i < xs.Count; i++)
            {
                var (x2, y2) = ConvertPoint(converter, xs[i], ys[i]);

                if (x1 != null && y1 != null && x2 != null && y2 != null)
                    graphics.DrawLine(x1.Value, y1.Value, x2.Value, y2.Value);

                x1 = x2;
                y1 = y2;
            }
        }

        /// <summary>Draws the data points as markers, without connecting them (a scatter).</summary>
        public void DrawPoints(PointPen pen, IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            var graphics = new VirtualGraphics(_graphics.GetImage(), pen);
            var converter = ScaledConverter(pen);

            for (int i = 0; i < xs.Count; i++)
            {
                var (x, y) = ConvertPoint(converter, xs[i], ys[i]);
                if (x != null && y != null)
                    graphics.DrawPoint(x.Value, y.Value);
            }
        }

        /// <summary>Draws a vertical stem from <paramref name="baseline"/> to each point (cell resolution), then the markers.</summary>
        public void DrawStems(LinePen stemPen, PointPen pointPen, IReadOnlyList<double> xs, IReadOnlyList<double> ys, double baseline)
        {
            int baseY = ConvertY(baseline);
            for (int i = 0; i < xs.Count; i++)
            {
                if (double.IsNaN(xs[i]) || double.IsNaN(ys[i]) || double.IsInfinity(xs[i]) || double.IsInfinity(ys[i]))
                    continue;
                _graphics.DrawVertical(stemPen, ConvertX(xs[i]), baseY, ConvertY(ys[i]));
            }

            DrawPoints(pointPen, xs, ys);
        }

        public void DrawVertical(LinePen pen, double x) =>
            _graphics.DrawVertical(pen, ConvertX(x));

        public void DrawVertical(LinePen pen, double x, double y) =>
            _graphics.DrawVertical(pen, ConvertX(x), ConvertY(y));

        public void DrawHorizontal(LinePen pen, double y) =>
            _graphics.DrawHorizontal(pen, ConvertY(y));

        public void DrawHorizontal(LinePen pen, double x, double y) =>
            _graphics.DrawHorizontal(pen, ConvertX(x), ConvertY(y));

        private int ConvertX(double x) => (int)Math.Round(_converter.ConvertX(x));
        private int ConvertY(double y) => (int)Math.Round(_converter.ConvertY(y));

        // The sub-cell converter: the drawing area scaled up by the brush's resolution, so a line/point is rasterized
        // at sub-cell precision and VirtualGraphics folds the sub-cells back into one rich glyph.
        private CoordinateConverter ScaledConverter(PointPen pen) => new CoordinateConverter(
            _converter.SourceX.Min, _converter.SourceX.Max,
            _converter.TargetX.Min * pen.Brush.HorizontalResolution, _converter.TargetX.Max * pen.Brush.HorizontalResolution,
            _converter.SourceY.Min, _converter.SourceY.Max,
            _converter.TargetY.Min * pen.Brush.VerticalResolution, _converter.TargetY.Max * pen.Brush.VerticalResolution);

        private static (int?, int?) ConvertPoint(CoordinateConverter converter, double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return (null, null);
            return (ConvertInfinity(x) ?? (int)Math.Round(converter.ConvertX(x)),
                ConvertInfinity(y) ?? (int)Math.Round(converter.ConvertY(y)));
        }

        private static int? ConvertInfinity(double value)
        {
            if (double.IsPositiveInfinity(value)) return int.MaxValue;
            if (double.IsNegativeInfinity(value)) return int.MinValue;
            return null;
        }
    }
}
