using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// An OHLC candlestick series: each point is drawn as a candle — a thin high/low wick with a thick open/close
    /// body — using half-cell box-drawing glyphs for sub-cell vertical precision (technique ported from termgraph).
    /// Candles are coloured by direction: <see cref="UpColor"/> when close ≥ open, else <see cref="DownColor"/>.
    /// </summary>
    public class CandleSeries : PlotElement
    {
        /// <summary>The X positions of the candles.</summary>
        public IReadOnlyList<double> Xs { get; }

        /// <summary>Open values.</summary>
        public IReadOnlyList<double> Opens { get; }

        /// <summary>High values (top of the wick).</summary>
        public IReadOnlyList<double> Highs { get; }

        /// <summary>Low values (bottom of the wick).</summary>
        public IReadOnlyList<double> Lows { get; }

        /// <summary>Close values.</summary>
        public IReadOnlyList<double> Closes { get; }

        /// <summary>Colour for an up candle (close ≥ open).</summary>
        public ConsoleGUI.Data.Color UpColor { get; }

        /// <summary>Colour for a down candle (close &lt; open).</summary>
        public ConsoleGUI.Data.Color DownColor { get; }

        public CandleSeries(
            IEnumerable<double> xs, IEnumerable<double> opens, IEnumerable<double> highs,
            IEnumerable<double> lows, IEnumerable<double> closes,
            ConsoleGUI.Data.Color upColor, ConsoleGUI.Data.Color downColor)
        {
            Xs = AsList(xs);
            Opens = AsList(opens);
            Highs = AsList(highs);
            Lows = AsList(lows);
            Closes = AsList(closes);

            int n = Xs.Count;
            if (Opens.Count != n || Highs.Count != n || Lows.Count != n || Closes.Count != n)
                throw new ArgumentException("All OHLC collections must have the same length as xs.");

            UpColor = upColor;
            DownColor = downColor;
        }

        // X-range from the positions, y-range spanning lows..highs.
        internal override Bounds? GetDataBounds() => Bounds.Union(Bounds.FromXY(Xs, Highs), Bounds.FromXY(Xs, Lows));

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawCandles(Xs, Opens, Highs, Lows, Closes, UpColor, DownColor);
    }
}
