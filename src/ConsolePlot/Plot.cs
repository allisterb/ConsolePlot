using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGUI.Api;
using ConsolePlot.Drawing;
using ConsolePlot.Drawing.Tools;
using ConsolePlot.Plotting;

namespace ConsolePlot
{
    /// <summary>
    /// Represents a plot that can be drawn on a console.
    /// </summary>
    public class Plot
    {
        
        private readonly ConsoleImage _image;
        private readonly PlotSettings _settings;

        /// <summary>
        /// Gets the axis settings for the plot.
        /// </summary>
        public AxisSettings Axis => _settings.Axis;

        /// <summary>
        /// Gets the grid settings for the plot.
        /// </summary>
        public GridSettings Grid => _settings.Grid;

        /// <summary>
        /// Gets the tick settings for the plot.
        /// </summary>
        public TickSettings Ticks => _settings.Ticks;

        /// <summary>A fixed (min, max) horizontal-axis range, or <see langword="null"/> to auto-scale; see
        /// <see cref="PlotSettings.FixedXRange"/>.</summary>
        public (double Min, double Max)? FixedXRange { get => _settings.FixedXRange; set => _settings.FixedXRange = value; }

        /// <summary>A fixed (min, max) vertical-axis range, or <see langword="null"/> to auto-scale.</summary>
        public (double Min, double Max)? FixedYRange { get => _settings.FixedYRange; set => _settings.FixedYRange = value; }

        /// <summary>A sliding horizontal window width, or <see langword="null"/>; see <see cref="PlotSettings.XWindow"/>.</summary>
        public double? XWindow { get => _settings.XWindow; set => _settings.XWindow = value; }

        /// <summary>
        /// Gets the collection of elements (series, scatter, stems, …) added to the plot.
        /// </summary>
        public List<PlotElement> Elements { get; } = new List<PlotElement>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Plot" /> class.
        /// </summary>
        /// <param name="width">The width of the plot in console characters.</param>
        /// <param name="height">The height of the plot in console characters.</param>
        /// <exception cref="ArgumentException">Thrown when width or height is not positive.</exception>
        public Plot(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and height must be positive.");
            }

            _image = new ConsoleImage(width, height);
            _settings = new PlotSettings();
        }

        /// <summary>
        /// Initializes a new <see cref="Plot"/> that draws straight into <paramref name="target"/> (its size sets the
        /// plot size) — no intermediate pixel buffer to copy out. Used by a host that owns the render buffer.
        /// </summary>
        public Plot(IConsoleBuffer target)
        {
            _image = new ConsoleImage(target);
            _settings = new PlotSettings();
        }

        /// <summary>
        /// Adds a new series to the plot.
        /// </summary>
        /// <param name="xs">The X values of the series.</param>
        /// <param name="ys">The Y values of the series.</param>
        /// <param name="pen">The pen to use for drawing the series (optional).</param>
        /// <returns>The <see cref="Plot" /> instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when xs and ys have different lengths.</exception>
        public Series AddSeries(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> ys,
            PointPen pen = default)
        {
            if (xs.Count != ys.Count)
            {
                throw new ArgumentException("X and Y collections must have the same length.");
            }

            if (pen.Equals(default(PointPen)))
                pen = new PointPen(_settings.DefaultGraphBrush ?? SystemPointBrushes.Braille, GetNextAvailableColor());

            var series = new Series(xs, ys, pen);
            Elements.Add(series);
            return series;
        }

        /// <summary>
        /// Adds a scatter series: the data points drawn as markers, without connecting lines.
        /// </summary>
        public ScatterSeries AddScatter(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> ys,
            PointPen pen = default)
        {
            if (xs.Count != ys.Count)
                throw new ArgumentException("X and Y collections must have the same length.");
            if (pen.Equals(default(PointPen)))
                pen = new PointPen(_settings.DefaultGraphBrush ?? SystemPointBrushes.Braille, GetNextAvailableColor());

            var series = new ScatterSeries(xs, ys, pen);
            Elements.Add(series);
            return series;
        }

        /// <summary>
        /// Adds a stem series: a vertical line from <paramref name="baseline"/> to each point, capped with a marker.
        /// </summary>
        public StemSeries AddStem(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> ys,
            PointPen pen = default,
            double baseline = 0,
            LinePen stemPen = null)
        {
            if (xs.Count != ys.Count)
                throw new ArgumentException("X and Y collections must have the same length.");
            if (pen.Equals(default(PointPen)))
                pen = new PointPen(_settings.DefaultGraphBrush ?? SystemPointBrushes.Braille, GetNextAvailableColor());

            var series = new StemSeries(xs, ys, pen, baseline, stemPen);
            Elements.Add(series);
            return series;
        }

        /// <summary>
        /// Adds a vertical bar series: each point drawn as a filled bar from <paramref name="baseline"/> to its value.
        /// </summary>
        public BarSeries AddBars(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> ys,
            ConsoleGUI.Data.Color color,
            double baseline = 0,
            double widthFraction = 0.8)
        {
            if (xs.Count != ys.Count)
                throw new ArgumentException("X and Y collections must have the same length.");

            var bars = new BarSeries(xs, ys, color, baseline, widthFraction);
            Elements.Add(bars);
            return bars;
        }

        /// <summary>
        /// Adds an OHLC candlestick series: each point drawn as a candle (high/low wick + open/close body), coloured
        /// by direction (close ≥ open uses <paramref name="upColor"/>, else <paramref name="downColor"/>).
        /// </summary>
        public CandleSeries AddCandles(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> opens,
            IReadOnlyCollection<double> highs,
            IReadOnlyCollection<double> lows,
            IReadOnlyCollection<double> closes,
            ConsoleGUI.Data.Color upColor,
            ConsoleGUI.Data.Color downColor)
        {
            var candles = new CandleSeries(xs, opens, highs, lows, closes, upColor, downColor);
            Elements.Add(candles);
            return candles;
        }

        /// <summary>
        /// Adds a box-and-whisker series: each entry is a Q1–Q3 box with a median line and whiskers to min/max.
        /// Callers supply the five-number summary per box.
        /// </summary>
        public BoxSeries AddBox(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> mins,
            IReadOnlyCollection<double> q1s,
            IReadOnlyCollection<double> medians,
            IReadOnlyCollection<double> q3s,
            IReadOnlyCollection<double> maxes,
            ConsoleGUI.Data.Color boxColor,
            ConsoleGUI.Data.Color medianColor,
            double widthFraction = 0.6)
        {
            var box = new BoxSeries(xs, mins, q1s, medians, q3s, maxes, boxColor, medianColor, widthFraction);
            Elements.Add(box);
            return box;
        }

        /// <summary>
        /// Adds a vertical error-bar series: each point (x, y) drawn as a whisker from <c>y − errLow</c> to
        /// <c>y + errHigh</c> with caps and a centre marker.
        /// </summary>
        public ErrorBarSeries AddErrorBars(
            IReadOnlyCollection<double> xs,
            IReadOnlyCollection<double> ys,
            IReadOnlyCollection<double> errLows,
            IReadOnlyCollection<double> errHighs,
            ConsoleGUI.Data.Color color,
            int capRadius = 1)
        {
            var bars = new ErrorBarSeries(xs, ys, errLows, errHighs, color, capRadius);
            Elements.Add(bars);
            return bars;
        }

        /// <summary>
        /// Adds a grouped (side-by-side) vertical bar element: each x holds one sub-bar per series in
        /// <paramref name="seriesValues"/>, coloured by <paramref name="colors"/>.
        /// </summary>
        public MultiBarSeries AddGroupedBars(
            IReadOnlyCollection<double> xs,
            IEnumerable<IEnumerable<double>> seriesValues,
            IEnumerable<ConsoleGUI.Data.Color> colors,
            double baseline = 0,
            double widthFraction = 0.8)
        {
            var bars = new MultiBarSeries(xs, seriesValues, colors, stacked: false, baseline, widthFraction);
            Elements.Add(bars);
            return bars;
        }

        /// <summary>
        /// Adds a stacked vertical bar element: at each x the series in <paramref name="seriesValues"/> are stacked
        /// from <paramref name="baseline"/>, coloured by <paramref name="colors"/>.
        /// </summary>
        public MultiBarSeries AddStackedBars(
            IReadOnlyCollection<double> xs,
            IEnumerable<IEnumerable<double>> seriesValues,
            IEnumerable<ConsoleGUI.Data.Color> colors,
            double baseline = 0,
            double widthFraction = 0.8)
        {
            var bars = new MultiBarSeries(xs, seriesValues, colors, stacked: true, baseline, widthFraction);
            Elements.Add(bars);
            return bars;
        }

        /// <summary>
        /// Adds a horizontal bar series: each category sits at a Y position and its bar grows along X from
        /// <paramref name="baseline"/> to its value.
        /// </summary>
        public HBarSeries AddHBars(
            IReadOnlyCollection<double> ys,
            IReadOnlyCollection<double> values,
            ConsoleGUI.Data.Color color,
            double baseline = 0,
            double widthFraction = 0.8)
        {
            if (ys.Count != values.Count)
                throw new ArgumentException("Y and value collections must have the same length.");

            var bars = new HBarSeries(ys, values, color, baseline, widthFraction);
            Elements.Add(bars);
            return bars;
        }

        /// <summary>
        /// Adds a heatmap: a grid of <paramref name="values"/> (rows × cols, row 0 at the top) tiled over the data
        /// rectangle [<paramref name="xMin"/>..<paramref name="xMax"/>] × [<paramref name="yMin"/>..
        /// <paramref name="yMax"/>], coloured by <paramref name="colorMap"/> over [<paramref name="vmin"/>,
        /// <paramref name="vmax"/>].
        /// </summary>
        public HeatSeries AddHeatmap(
            IEnumerable<IEnumerable<double>> values,
            double xMin, double xMax, double yMin, double yMax,
            double vmin, double vmax,
            Func<double, ConsoleGUI.Data.Color> colorMap,
            Func<double, string> cellText = null)
        {
            var heat = new HeatSeries(values, xMin, xMax, yMin, yMax, vmin, vmax, colorMap, cellText);
            Elements.Add(heat);
            return heat;
        }

        /// <summary>
        /// Adds a text annotation anchored to the data point (<paramref name="x"/>, <paramref name="y"/>). Does not
        /// affect the plot's data bounds.
        /// </summary>
        public PointLabel AddLabel(
            double x, double y, string text,
            ConsoleGUI.Data.Color foreground,
            ConsoleGUI.Data.Color? background = null,
            LabelAlignment alignment = LabelAlignment.Center,
            int offsetX = 0,
            int offsetY = 1)
        {
            var label = new PointLabel(x, y, text, foreground, background, alignment, offsetX, offsetY);
            Elements.Add(label);
            return label;
        }

        /// <summary>
        /// Draws the plot on the console image.
        /// </summary>
        public void Draw()
        {
            _settings.Validate();
            var plotData = PlotData.Calculate(Elements, _settings, _image.Width, _image.Height);
            var renderer = new PlotRenderer(_image, plotData, _settings);

            renderer.Draw();
        }

        /// <summary>
        /// Gets the underlying <see cref="ConsoleImage" />.
        /// </summary>
        /// <returns>The <see cref="ConsoleImage" /> this plotting context is drawing on.</returns>
        public ConsoleImage GetImage() => _image;

        /// <summary>
        /// Renders the plot on the console.
        /// </summary>
       
        public virtual void Render()
        {
            for (int y = _image.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _image.Width; x++)
                {
                    var cell = _image.GetCharacter(x, y);
                    Console.ForegroundColor = GetNearestConsoleColor(cell.Foreground ?? default);
                    Console.Write(cell.Content ?? ' ');
                }
                Console.WriteLine();
            }
            Console.ResetColor();
        }
        
        public static readonly ConsoleColor[] AvailableColors = new ConsoleColor[]
        {
            ConsoleColor.Black,
            ConsoleColor.DarkBlue,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkRed,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkYellow,
            ConsoleColor.Gray,
            ConsoleColor.DarkGray,
            ConsoleColor.Blue,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Red,
            ConsoleColor.Magenta,
            ConsoleColor.Yellow,
            ConsoleColor.White
        };

        private ConsoleColor GetNextAvailableColor()
        {
            var usedColors = new HashSet<ConsoleGUI.Data.Color>();

            // Include colors of existing point-based elements (series/scatter/stem)
            usedColors.UnionWith(Elements.OfType<Series>().Select(s => s.Pen.Color));

            // Include axis color if visible
            if (_settings.Axis.IsVisible)
            {
                usedColors.Add(_settings.Axis.Pen.Color);
            }

            // Include grid color if visible
            if (_settings.Grid.IsVisible)
            {
                usedColors.Add(_settings.Grid.Pen.Color);
            }

            // Return the first available color
            return AvailableColors.First(c => !usedColors.Contains(c));
        }

        public static ConsoleColor GetNearestConsoleColor(ConsoleGUI.Data.Color color)
        {
            if (Math.Max(Math.Max(color.Red, color.Green), color.Blue) - Math.Min(Math.Min(color.Red, color.Green), color.Blue) < 32)
            {
                int brightness = ((int)color.Red + (int)color.Green + (int)color.Blue) / 3;
                if (brightness < 64) return ConsoleColor.Black;
                if (brightness < 160) return ConsoleColor.DarkGray;
                if (brightness < 224) return ConsoleColor.Gray;
                return ConsoleColor.White;
            }
            int index = (color.Red > 128 | color.Green > 128 | color.Blue > 128) ? 8 : 0;
            index |= (color.Red > 64) ? 4 : 0;
            index |= (color.Green > 64) ? 2 : 0;
            index |= (color.Blue > 64) ? 1 : 0;
            return (ConsoleColor)index;
        }
    }
}