using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        // Bottom-anchored partial-fill glyphs 1/8..8/8 (index 1..8), for a vertical bar's fractional top cell.
        private static readonly char[] EighthUp = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        // Left-anchored partial-fill glyphs 1/8..8/8 (index 1..8), for a horizontal bar's fractional right cell.
        private static readonly char[] EighthRight = { ' ', '▏', '▎', '▍', '▌', '▋', '▊', '▉', '█' };

        public GraphGraphics(ConsoleGraphics graphics, CoordinateConverter converter)
        {
            _graphics = graphics;
            _converter = converter;
        }

        public void DrawLines(PointPen pen, IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            var graphics = new VirtualGraphics(_graphics.GetImage(), pen);
            var converter = ScaledConverter(pen);
            int n = xs.Count;

            int? x1 = null, y1 = null;

            // Fast path: index the backing array/list directly — avoids a per-point IReadOnlyList indexer interface
            // call (and lets the JIT elide bounds checks). Covers static series (double[]) and live series
            // (List<double>, via CollectionsMarshal). Any other IReadOnlyList falls through to the indexer below.
            if (TryAsSpan(xs, out var sx) && TryAsSpan(ys, out var sy))
            {
                for (int i = 0; i < n; i++)
                {
                    var (x2, y2) = ConvertPoint(converter, sx[i], sy[i]);
                    if (x1 != null && y1 != null && x2 != null && y2 != null)
                        graphics.DrawLine(x1.Value, y1.Value, x2.Value, y2.Value);
                    x1 = x2;
                    y1 = y2;
                }
                return;
            }

            for (int i = 0; i < n; i++)
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
            int n = xs.Count;

            // Fast path — see DrawLines.
            if (TryAsSpan(xs, out var sx) && TryAsSpan(ys, out var sy))
            {
                for (int i = 0; i < n; i++)
                {
                    var (x, y) = ConvertPoint(converter, sx[i], sy[i]);
                    if (x != null && y != null)
                        graphics.DrawPoint(x.Value, y.Value);
                }
                return;
            }

            for (int i = 0; i < n; i++)
            {
                var (x, y) = ConvertPoint(converter, xs[i], ys[i]);
                if (x != null && y != null)
                    graphics.DrawPoint(x.Value, y.Value);
            }
        }

        // Exposes the backing storage of a double[] or List<double> as a span so the draw loops can index it
        // directly (no IReadOnlyList indexer dispatch). Returns false for any other IReadOnlyList — the caller then
        // uses the interface indexer. Safe here: drawing runs single-threaded on the UI thread, so the list isn't
        // mutated mid-iteration.
        private static bool TryAsSpan(IReadOnlyList<double> values, out ReadOnlySpan<double> span)
        {
            switch (values)
            {
                case double[] a: span = a; return true;
                case List<double> l: span = CollectionsMarshal.AsSpan(l); return true;
                default: span = default; return false;
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

        /// <summary>
        /// Draws grouped (side-by-side) vertical bars: at each x the slot is split into one sub-bar per series, so
        /// <paramref name="seriesValues"/>[j] is drawn in the j-th sub-slot coloured <paramref name="colors"/>[j].
        /// </summary>
        public void DrawGroupedBars(
            IReadOnlyList<ConsoleGUI.Data.Color> colors, IReadOnlyList<double> xs,
            IReadOnlyList<IReadOnlyList<double>> seriesValues, double baseline, double widthFraction)
        {
            int baseRow = ConvertY(baseline);
            int n = xs.Count, k = seriesValues.Count;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(xs[i])) continue;
                var (colStart, colEnd) = SlotColumns(i, xs, n, widthFraction);
                int slotW = colEnd - colStart;
                for (int j = 0; j < k; j++)
                {
                    if (i >= seriesValues[j].Count || !IsFinite(seriesValues[j][i])) continue;
                    // Sub-slot j of k, split by rounding so the k sub-bars tile the slot exactly.
                    int subStart = colStart + (int)Math.Round((double)slotW * j / k);
                    int subEnd = colStart + (int)Math.Round((double)slotW * (j + 1) / k);
                    if (subEnd <= subStart) subEnd = subStart + 1;
                    double topExact = _converter.ConvertY(seriesValues[j][i]);
                    for (int col = subStart; col < subEnd; col++)
                        FillColumn(col, baseRow, topExact, colors[j]);
                }
            }
        }

        /// <summary>
        /// Draws stacked vertical bars: at each x the series are stacked from <paramref name="baseline"/>, each
        /// segment filling the full slot width in <paramref name="colors"/>[j]. Segments are full cells between
        /// rounded cumulative boundaries so they abut exactly (no sub-cell tops, which wouldn't align across a stack).
        /// </summary>
        public void DrawStackedBars(
            IReadOnlyList<ConsoleGUI.Data.Color> colors, IReadOnlyList<double> xs,
            IReadOnlyList<IReadOnlyList<double>> seriesValues, double baseline, double widthFraction)
        {
            int n = xs.Count, k = seriesValues.Count;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(xs[i])) continue;
                var (colStart, colEnd) = SlotColumns(i, xs, n, widthFraction);

                double cumulative = baseline;
                int prevRow = ConvertY(baseline);
                for (int j = 0; j < k; j++)
                {
                    if (i >= seriesValues[j].Count || !IsFinite(seriesValues[j][i])) continue;
                    cumulative += seriesValues[j][i];
                    int thisRow = ConvertY(cumulative);
                    int lo = Math.Min(prevRow, thisRow), hi = Math.Max(prevRow, thisRow);
                    for (int col = colStart; col < colEnd; col++)
                        for (int r = lo; r <= hi; r++) SolidCell(col, r, colors[j]);
                    prevRow = thisRow;
                }
            }
        }

        /// <summary>
        /// Draws filled horizontal bars from <paramref name="baseline"/> to each value: positions are on the Y axis
        /// (each bar owns a row slot) and the value extends along X, with a left-anchored eighth-block for the
        /// fractional right cell.
        /// </summary>
        public void DrawHBars(ConsoleGUI.Data.Color color, IReadOnlyList<double> ys, IReadOnlyList<double> values, double baseline, double widthFraction)
        {
            int baseCol = ConvertX(baseline);
            int n = ys.Count;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(ys[i]) || !IsFinite(values[i])) continue;
                var (rowStart, rowEnd) = SlotRows(i, ys, n, widthFraction);
                double rightExact = _converter.ConvertX(values[i]);
                for (int row = rowStart; row < rowEnd; row++)
                    FillRow(row, baseCol, rightExact, color);
            }
        }

        /// <summary>
        /// Draws a heatmap: a grid of <paramref name="values"/> (rows × cols, row 0 at the top) tiled over the
        /// data rectangle [<paramref name="xMin"/>..<paramref name="xMax"/>] × [<paramref name="yMin"/>..
        /// <paramref name="yMax"/>], each cell filled with the colour from <paramref name="colorMap"/> for its value
        /// normalised into [<paramref name="vmin"/>, <paramref name="vmax"/>]. NaN/∞ cells are left blank.
        /// </summary>
        public void DrawHeat(
            IReadOnlyList<IReadOnlyList<double>> values, double xMin, double xMax, double yMin, double yMax,
            double vmin, double vmax, Func<double, ConsoleGUI.Data.Color> colorMap, Func<double, string> cellText = null)
        {
            int rows = values.Count;
            if (rows == 0) return;
            int cols = values[0].Count;
            if (cols == 0) return;

            double range = vmax - vmin;
            double dx = (xMax - xMin) / cols;
            double dy = (yMax - yMin) / rows;

            for (int r = 0; r < rows; r++)
            {
                var rowVals = values[r];
                // Row 0 is the top of the grid, so it maps to the data-y band just below yMax. Image y increases
                // upward, so the band's higher data-y (yMax − r·dy) is the higher pixel row.
                int rowTop = ConvertY(yMax - r * dy);
                int rowBot = ConvertY(yMax - (r + 1) * dy);
                if (rowTop < rowBot) (rowTop, rowBot) = (rowBot, rowTop);
                if (rowTop <= rowBot) rowTop = rowBot + 1;

                for (int c = 0; c < cols && c < rowVals.Count; c++)
                {
                    double v = rowVals[c];
                    if (!IsFinite(v)) continue;

                    // Edges rounded consistently so adjacent cells share a boundary and tile without gaps.
                    int colL = ConvertX(xMin + c * dx);
                    int colR = ConvertX(xMin + (c + 1) * dx);
                    if (colR <= colL) colR = colL + 1;

                    double t = range > 0 ? (v - vmin) / range : 0.5;
                    var color = colorMap(Math.Clamp(t, 0.0, 1.0));
                    for (int col = colL; col < colR; col++)
                        for (int row = rowBot; row < rowTop; row++)
                            SolidCell(col, row, color);

                    // Optional value text, centred in the cell with a contrasting colour on the cell's own colour
                    // as background (change B), drawn only when it fits the cell width.
                    if (cellText != null)
                    {
                        string label = cellText(v);
                        int cellW = colR - colL;
                        if (!string.IsNullOrEmpty(label) && label.Length <= cellW)
                        {
                            int startCol = colL + (cellW - label.Length) / 2;
                            int midRow = (rowBot + rowTop - 1) / 2;
                            _graphics.DrawText(label, ContrastText(color), color, startCol, midRow);
                        }
                    }
                }
            }
        }

        // Readable text colour for a filled cell: dark on light backgrounds, light on dark, by perceived luminance.
        private static ConsoleGUI.Data.Color ContrastText(ConsoleGUI.Data.Color bg)
        {
            double luminance = 0.299 * bg.Red + 0.587 * bg.Green + 0.114 * bg.Blue;
            return luminance > 140 ? new ConsoleGUI.Data.Color(20, 20, 20) : new ConsoleGUI.Data.Color(240, 240, 240);
        }

        // The half-open [colStart, colEnd) column range one slotted element (bar/box) occupies at index i along the
        // X axis. See SlotRange for the slot math.
        private (int colStart, int colEnd) SlotColumns(int i, IReadOnlyList<double> xs, int n, double widthFraction) =>
            SlotRange(
                _converter.ConvertX(xs[i]),
                i > 0 && IsFinite(xs[i - 1]) ? _converter.ConvertX(xs[i - 1]) : (double?)null,
                i < n - 1 && IsFinite(xs[i + 1]) ? _converter.ConvertX(xs[i + 1]) : (double?)null,
                widthFraction);

        // The half-open [rowStart, rowEnd) row range a slotted element occupies at index i along the Y axis (for
        // horizontal bars).
        private (int rowStart, int rowEnd) SlotRows(int i, IReadOnlyList<double> ys, int n, double widthFraction) =>
            SlotRange(
                _converter.ConvertY(ys[i]),
                i > 0 && IsFinite(ys[i - 1]) ? _converter.ConvertY(ys[i - 1]) : (double?)null,
                i < n - 1 && IsFinite(ys[i + 1]) ? _converter.ConvertY(ys[i + 1]) : (double?)null,
                widthFraction);

        // The half-open [start, end) cell range one slotted element occupies around fractional pixel position
        // `center`. The element owns a slot bounded by the midpoints to its neighbours; the drawn extent is that slot
        // scaled by widthFraction and centred. Half-open ranges make full-width (fraction 1) elements tile exactly —
        // no gaps or overlaps as the plot is resized. Edge elements mirror their one neighbour's gap; a lone element
        // uses a small default.
        private static (int start, int end) SlotRange(double center, double? prev, double? next, double widthFraction)
        {
            double leftGap = prev.HasValue ? center - prev.Value : (next.HasValue ? next.Value - center : DefaultBarGap);
            double rightGap = next.HasValue ? next.Value - center : (prev.HasValue ? center - prev.Value : DefaultBarGap);

            int start = (int)Math.Round(center - leftGap / 2 * widthFraction);
            int end = (int)Math.Round(center + rightGap / 2 * widthFraction);   // exclusive
            if (end <= start) end = start + 1;                                   // always at least one cell
            return (start, end);
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
                // Full interior cells are solid (fg == bg, no banding); the fractional top keeps a transparent
                // background so the eighth-block glyph shows the empty part above the fill.
                for (int r = baseRow; r < topRow; r++) SolidCell(col, r, color);
                double frac = topExact - topRow;
                if (frac > 0.05) Cell(col, topRow, EighthUp[Math.Clamp((int)Math.Ceiling(frac * 8), 1, 8)], color);
            }
            else
            {
                for (int r = (int)Math.Ceiling(topExact); r <= baseRow; r++) SolidCell(col, r, color);
            }
        }

        // Fill one horizontal-bar row. X is not flipped, so a rightward bar (value right of the baseline) fills full
        // cells then a left-anchored eighth-block for the fractional right cell. A leftward bar fills full cells only.
        private void FillRow(int row, int baseCol, double rightExact, ConsoleGUI.Data.Color color)
        {
            int rightCol = (int)Math.Floor(rightExact);
            if (rightCol >= baseCol)
            {
                // Full interior cells are solid; the fractional right cell keeps a transparent background so the
                // left-anchored eighth-block shows the empty part to its right.
                for (int c = baseCol; c < rightCol; c++) SolidCell(c, row, color);
                double frac = rightExact - rightCol;
                if (frac > 0.05) Cell(rightCol, row, EighthRight[Math.Clamp((int)Math.Ceiling(frac * 8), 1, 8)], color);
            }
            else
            {
                for (int c = (int)Math.Ceiling(rightExact); c <= baseCol; c++) SolidCell(c, row, color);
            }
        }

        private void Cell(int col, int row, char ch, ConsoleGUI.Data.Color color) =>
            _graphics.DrawPoint(new ConsolePointPen(new ConsolePointBrush(ch), color), col, row);

        // A fully solid coloured cell: the block glyph in the colour AND the same colour as the cell background, so
        // any sub-glyph gap the terminal font leaves between rows is filled with the same colour (no banding) — used
        // for heatmap cells. "█" is an interned literal, so this doesn't allocate per call.
        private void SolidCell(int col, int row, ConsoleGUI.Data.Color color) =>
            _graphics.DrawText("█", color, color, col, row);

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
        // Each cell contributes vRes/hRes sub-pixels, so the drawing area's inclusive cell range [Bottom, Top] spans
        // the sub-pixel range [Bottom*res, Top*res + (res-1)] — the "+ (res-1)" reaches the far sub-pixels of the last
        // cell. Without it the top (vRes-1) sub-rows and right (hRes-1) sub-cols are never used, so a point's sub-cell
        // position is biased toward the cell's bottom-left and (e.g.) data-max lights a cell's bottom braille dots.
        private CoordinateConverter ScaledConverter(PointPen pen) => new CoordinateConverter(
            _converter.SourceX.Min, _converter.SourceX.Max,
            _converter.TargetX.Min * pen.Brush.HorizontalResolution,
            _converter.TargetX.Max * pen.Brush.HorizontalResolution + (pen.Brush.HorizontalResolution - 1),
            _converter.SourceY.Min, _converter.SourceY.Max,
            _converter.TargetY.Min * pen.Brush.VerticalResolution,
            _converter.TargetY.Max * pen.Brush.VerticalResolution + (pen.Brush.VerticalResolution - 1));

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
