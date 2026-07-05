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

        // Bottom-anchored partial-fill glyphs 1/8..8/8 (index 1..8), for a bar's fractional top cell.
        private static readonly char[] EighthUp = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

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

        /// <summary>
        /// Draws filled vertical bars from <paramref name="baseline"/> to each value. Interior cells are full blocks;
        /// the bar top gets sub-cell precision from an eighth-block glyph. Bars are foreground colour fills.
        /// </summary>
        public void DrawBars(ConsoleGUI.Data.Color color, IReadOnlyList<double> xs, IReadOnlyList<double> ys, double baseline, double widthFraction)
        {
            int baseRow = ConvertY(baseline);
            int half = BarHalfWidth(xs, widthFraction);
            for (int i = 0; i < xs.Count; i++)
            {
                if (double.IsNaN(xs[i]) || double.IsNaN(ys[i]) || double.IsInfinity(xs[i]) || double.IsInfinity(ys[i]))
                    continue;
                int xc = ConvertX(xs[i]);
                double topExact = _converter.ConvertY(ys[i]);
                for (int col = xc - half; col <= xc + half; col++)
                    FillColumn(col, baseRow, topExact, color);
            }
        }

        // Fill one bar column. Image y increases upward, so an upward bar (top above the baseline) fills full cells
        // then a bottom-anchored eighth-block for the fractional top cell — which reads correctly after PlotImage's
        // vertical flip. A downward bar (value below baseline) fills full cells only (no sub-cell top).
        private void FillColumn(int col, int baseRow, double topExact, ConsoleGUI.Data.Color color)
        {
            int topRow = (int)Math.Floor(topExact);
            if (topRow >= baseRow)
            {
                for (int r = baseRow; r < topRow; r++) Cell(col, r, '█', color);
                double frac = topExact - topRow;
                if (frac > 0.05) Cell(col, topRow, EighthUp[Math.Clamp((int)Math.Ceiling(frac * 8), 1, 8)], color);
            }
            else
            {
                for (int r = (int)Math.Ceiling(topExact); r <= baseRow; r++) Cell(col, r, '█', color);
            }
        }

        private void Cell(int col, int row, char ch, ConsoleGUI.Data.Color color) =>
            _graphics.DrawPoint(new ConsolePointPen(new ConsolePointBrush(ch), color), col, row);

        // Bar half-width in cells: a fraction of the smallest gap between adjacent bar positions (a lone bar gets a
        // small default), so bars fill their slot without overlapping neighbours.
        private int BarHalfWidth(IReadOnlyList<double> xs, double widthFraction)
        {
            int gap = int.MaxValue;
            for (int i = 1; i < xs.Count; i++)
            {
                if (double.IsNaN(xs[i]) || double.IsNaN(xs[i - 1])) continue;
                int g = Math.Abs(ConvertX(xs[i]) - ConvertX(xs[i - 1]));
                if (g > 0 && g < gap) gap = g;
            }
            if (gap == int.MaxValue) gap = 3;
            int w = Math.Max(1, (int)(gap * widthFraction));
            return (w - 1) / 2;
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
