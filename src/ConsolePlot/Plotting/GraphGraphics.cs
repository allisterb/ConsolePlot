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
                var (colStart, colEnd) = SlotColumns(i, xs, n, widthFraction);
                double topExact = _converter.ConvertY(ys[i]);
                for (int col = colStart; col < colEnd; col++)
                    FillColumn(col, baseRow, topExact, color);
            }
        }

        // The half-open [colStart, colEnd) column range one slotted element (bar/box) occupies at index i. Each
        // element owns a slot bounded by the midpoints to its neighbours (fractional pixels); the drawn extent is
        // that slot scaled by widthFraction and centred. Half-open ranges make full-width (fraction 1) elements
        // tile exactly — no gaps or overlaps as the plot is resized. Edge elements mirror their one neighbour's gap;
        // a lone element uses a small default.
        private (int colStart, int colEnd) SlotColumns(int i, IReadOnlyList<double> xs, int n, double widthFraction)
        {
            double cx = _converter.ConvertX(xs[i]);
            double? prev = i > 0 && IsFinite(xs[i - 1]) ? _converter.ConvertX(xs[i - 1]) : (double?)null;
            double? next = i < n - 1 && IsFinite(xs[i + 1]) ? _converter.ConvertX(xs[i + 1]) : (double?)null;
            double leftGap = prev.HasValue ? cx - prev.Value : (next.HasValue ? next.Value - cx : DefaultBarGap);
            double rightGap = next.HasValue ? next.Value - cx : (prev.HasValue ? cx - prev.Value : DefaultBarGap);

            int colStart = (int)Math.Round(cx - leftGap / 2 * widthFraction);
            int colEnd = (int)Math.Round(cx + rightGap / 2 * widthFraction);   // exclusive
            if (colEnd <= colStart) colEnd = colStart + 1;                      // always at least one column
            return (colStart, colEnd);
        }

        /// <summary>
        /// Draws box-and-whisker boxes (one slot each): a Q1–Q3 box with a median line, whiskers to min/max with
        /// caps. Box-drawing glyphs at cell resolution. <paramref name="medianColor"/> colours the median line;
        /// everything else uses <paramref name="boxColor"/>.
        /// </summary>
        public void DrawBoxes(
            ConsoleGUI.Data.Color boxColor, ConsoleGUI.Data.Color medianColor,
            IReadOnlyList<double> xs, IReadOnlyList<double> mins, IReadOnlyList<double> q1s,
            IReadOnlyList<double> medians, IReadOnlyList<double> q3s, IReadOnlyList<double> maxes, double widthFraction)
        {
            int n = xs.Count;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(xs[i]) || !IsFinite(mins[i]) || !IsFinite(q1s[i]) || !IsFinite(medians[i]) || !IsFinite(q3s[i]) || !IsFinite(maxes[i]))
                    continue;

                var (colStart, colEnd) = SlotColumns(i, xs, n, widthFraction);
                int colLast = colEnd - 1;             // inclusive last box column
                int center = ConvertX(xs[i]);

                // Image y increases upward, so a larger value maps to a larger row: rMax ≥ rQ3 ≥ rMed ≥ rQ1 ≥ rMin.
                int rMax = ConvertY(maxes[i]), rQ3 = ConvertY(q3s[i]), rMed = ConvertY(medians[i]);
                int rQ1 = ConvertY(q1s[i]), rMin = ConvertY(mins[i]);

                // Whiskers: a centred vertical line between the box and each cap.
                for (int r = rQ3 + 1; r < rMax; r++) Cell(center, r, '│', boxColor);
                for (int r = rMin + 1; r < rQ1; r++) Cell(center, r, '│', boxColor);

                // Whisker caps span the box width; the centre tees toward the whisker.
                for (int c = colStart; c <= colLast; c++) Cell(c, rMax, c == center && rMax > rQ3 ? '┬' : '─', boxColor);
                for (int c = colStart; c <= colLast; c++) Cell(c, rMin, c == center && rMin < rQ1 ? '┴' : '─', boxColor);

                DrawBox(colStart, colLast, rQ1, rQ3, boxColor);

                // Where the whiskers meet the box, tee the box edge toward them (only when the box is wide enough
                // that the centre isn't a corner).
                if (colStart < center && center < colLast)
                {
                    if (rMax > rQ3) Cell(center, rQ3, '┬', boxColor);
                    if (rMin < rQ1) Cell(center, rQ1, '┴', boxColor);
                }

                // Median line across the box, teeing into the sides.
                if (colLast <= colStart)
                    Cell(colStart, rMed, '─', medianColor);
                else
                {
                    Cell(colStart, rMed, '├', medianColor);
                    Cell(colLast, rMed, '┤', medianColor);
                    for (int c = colStart + 1; c < colLast; c++) Cell(c, rMed, '─', medianColor);
                }
            }
        }

        // A rectangle from (colStart..colLast) × rows [rBot, rTop] (rBot ≤ rTop in image rows). Degenerates to a
        // single vertical line when too thin, or a horizontal line when collapsed in height.
        private void DrawBox(int colStart, int colLast, int rBot, int rTop, ConsoleGUI.Data.Color color)
        {
            if (colLast <= colStart)
            {
                for (int r = rBot; r <= rTop; r++) Cell(colStart, r, '│', color);
                return;
            }
            if (rTop <= rBot)
            {
                for (int c = colStart; c <= colLast; c++) Cell(c, rBot, '─', color);
                return;
            }

            Cell(colStart, rTop, '┌', color); Cell(colLast, rTop, '┐', color);
            Cell(colStart, rBot, '└', color); Cell(colLast, rBot, '┘', color);
            for (int c = colStart + 1; c < colLast; c++) { Cell(c, rTop, '─', color); Cell(c, rBot, '─', color); }
            for (int r = rBot + 1; r < rTop; r++) { Cell(colStart, r, '│', color); Cell(colLast, r, '│', color); }
        }

        /// <summary>
        /// Draws vertical error bars at each point: a whisker from y−errLow to y+errHigh with horizontal caps
        /// (<paramref name="capRadius"/> cells either side) and a centre marker. Cell resolution, full RGB colour.
        /// </summary>
        public void DrawErrorBars(
            ConsoleGUI.Data.Color color, IReadOnlyList<double> xs, IReadOnlyList<double> ys,
            IReadOnlyList<double> errLows, IReadOnlyList<double> errHighs, int capRadius)
        {
            for (int i = 0; i < xs.Count; i++)
            {
                if (!IsFinite(xs[i]) || !IsFinite(ys[i]) || !IsFinite(errLows[i]) || !IsFinite(errHighs[i]))
                    continue;

                int col = ConvertX(xs[i]);
                int rTop = ConvertY(ys[i] + Math.Abs(errHighs[i]));   // larger value → larger row
                int rBot = ConvertY(ys[i] - Math.Abs(errLows[i]));
                int rMid = ConvertY(ys[i]);
                if (rTop < rBot) (rTop, rBot) = (rBot, rTop);

                for (int r = rBot; r <= rTop; r++) Cell(col, r, '│', color);
                if (rTop > rBot)
                {
                    DrawCap(col, rTop, capRadius, '┬', color);   // whisker descends from the top cap
                    DrawCap(col, rBot, capRadius, '┴', color);   // whisker ascends from the bottom cap
                }
                Cell(col, rMid, '┼', color);                     // centre marker at the data point
            }
        }

        // A horizontal cap of half-width capRadius centred on col, with centreGlyph where the whisker meets it.
        private void DrawCap(int col, int row, int capRadius, char centreGlyph, ConsoleGUI.Data.Color color)
        {
            for (int c = col - capRadius; c <= col + capRadius; c++)
                Cell(c, row, c == col ? centreGlyph : '─', color);
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
