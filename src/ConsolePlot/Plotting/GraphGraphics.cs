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
            int n = xs.Count;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(xs[i]) || !IsFinite(ys[i]))
                    continue;

                // Each bar owns a slot bounded by the midpoints to its neighbours (fractional pixels). The drawn bar
                // is that slot scaled by widthFraction and centred; using half-open [start, end) column ranges makes
                // full-width (fraction 1) bars tile exactly — no gaps or overlaps as the plot is resized. Edge bars
                // mirror their one neighbour's gap; a lone bar uses a small default.
                double cx = _converter.ConvertX(xs[i]);
                double? prev = i > 0 && IsFinite(xs[i - 1]) ? _converter.ConvertX(xs[i - 1]) : (double?)null;
                double? next = i < n - 1 && IsFinite(xs[i + 1]) ? _converter.ConvertX(xs[i + 1]) : (double?)null;
                double leftGap = prev.HasValue ? cx - prev.Value : (next.HasValue ? next.Value - cx : DefaultBarGap);
                double rightGap = next.HasValue ? next.Value - cx : (prev.HasValue ? cx - prev.Value : DefaultBarGap);

                int colStart = (int)Math.Round(cx - leftGap / 2 * widthFraction);
                int colEnd = (int)Math.Round(cx + rightGap / 2 * widthFraction);   // exclusive
                if (colEnd <= colStart) colEnd = colStart + 1;                      // always at least one column

                double topExact = _converter.ConvertY(ys[i]);
                for (int col = colStart; col < colEnd; col++)
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

        /// <summary>
        /// Draws OHLC candlesticks (one column each): a thin high/low wick with a thick open/close body, using
        /// half-cell box glyphs for sub-cell precision. Each candle is coloured by direction (close ≥ open ? up : down).
        /// </summary>
        public void DrawCandles(
            IReadOnlyList<double> xs, IReadOnlyList<double> opens, IReadOnlyList<double> highs,
            IReadOnlyList<double> lows, IReadOnlyList<double> closes,
            ConsoleGUI.Data.Color upColor, ConsoleGUI.Data.Color downColor)
        {
            for (int i = 0; i < xs.Count; i++)
            {
                if (!IsFinite(xs[i]) || !IsFinite(opens[i]) || !IsFinite(highs[i]) || !IsFinite(lows[i]) || !IsFinite(closes[i]))
                    continue;

                int col = ConvertX(xs[i]);
                double o = opens[i], c = closes[i];
                double ts = _converter.ConvertY(highs[i]);          // high  (top of wick)
                double tc = _converter.ConvertY(Math.Max(o, c));    // body top
                double bc = _converter.ConvertY(Math.Min(o, c));    // body bottom
                double bs = _converter.ConvertY(lows[i]);           // low   (bottom of wick)
                var color = c >= o ? upColor : downColor;

                for (int r = (int)Math.Floor(bs); r <= (int)Math.Ceiling(ts); r++)
                {
                    char glyph = CandleGlyph(r, ts, tc, bc, bs);
                    if (glyph != ' ') Cell(col, r, glyph, color);
                }
            }
        }

        // Ported from termgraph's CandleStickGraph._render_candle_at: pick the half-cell box glyph for pixel row `hu`
        // from where the candle's high (ts), body top (tc), body bottom (bc) and low (bs) — all fractional pixel rows
        // — fall relative to the cell. Wick glyphs │╷╵, body glyphs ┃╽╿╻╹. Both this and PlotImage's flip end with
        // high-at-top, so the direct port renders right-side-up.
        private static char CandleGlyph(int hu, double ts, double tc, double bc, double bs)
        {
            if (Math.Ceiling(ts) >= hu && hu >= Math.Floor(tc))     // upper region: body top → high (wick above body)
            {
                if (tc - hu > 0.75) return '┃';
                if (tc - hu > 0.25) return ts - hu > 0.75 ? '╽' : '╻';
                if (ts - hu > 0.75) return '│';
                if (ts - hu > 0.25) return '╷';
                return ' ';
            }
            if (Math.Floor(tc) >= hu && hu >= Math.Ceiling(bc))     // body
                return '┃';
            if (Math.Ceiling(bc) >= hu && hu >= Math.Floor(bs))     // lower region: low → body bottom (wick below body)
            {
                if (bc - hu < 0.25) return '┃';
                if (bc - hu < 0.75) return bs - hu < 0.25 ? '╿' : '╹';
                if (bs - hu < 0.25) return '│';
                if (bs - hu < 0.75) return '╵';
                return ' ';
            }
            return ' ';
        }

        // The slot width (in cells) used for a bar that has no neighbours to measure a gap from.
        private const double DefaultBarGap = 3.0;

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Draws a text label anchored to the data point (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void DrawLabel(double x, double y, string text, ConsoleGUI.Data.Color fg, ConsoleGUI.Data.Color? bg, LabelAlignment alignment, int offsetX, int offsetY)
        {
            if (string.IsNullOrEmpty(text)) return;

            int col = ConvertX(x) + offsetX;
            int row = ConvertY(y) + offsetY;   // image y increases upward, so +offsetY places the label above the point
            int start = alignment switch
            {
                LabelAlignment.Center => col - text.Length / 2,
                LabelAlignment.Right => col - text.Length + 1,
                _ => col,
            };
            _graphics.DrawText(text, fg, bg, start, row);
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
