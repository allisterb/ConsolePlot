using System;
using System.Collections.Generic;
using ConsolePlot.Drawing;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Represents the calculated data for plotting.
    /// </summary>
    internal class PlotData
    {
        /// <summary>
        /// Gets the axis intersection point.
        /// </summary>
        public Point Axis { get; }

        /// <summary>
        /// Gets the data bounds of the plot.
        /// </summary>
        public Bounds DataBounds { get; }

        /// <summary>
        /// Gets the drawing area of the plot.
        /// </summary>
        public Rectangle DrawingArea { get; }

        /// <summary>
        /// Gets the elements to be plotted.
        /// </summary>
        public List<PlotElement> Elements { get; }

        /// <summary>
        /// Gets the X-axis ticks.
        /// </summary>
        public List<Tick> XTicks { get; }

        /// <summary>
        /// Gets the Y-axis ticks.
        /// </summary>
        public List<Tick> YTicks { get; }

        private PlotData(
            Bounds dataBounds,
            Rectangle drawingArea,
            List<Tick> xTicks,
            List<Tick> yTicks,
            Point axis,
            List<PlotElement> elements)
        {
            DataBounds = dataBounds;
            DrawingArea = drawingArea;
            XTicks = xTicks;
            YTicks = yTicks;
            Axis = axis;
            Elements = elements;
        }

        /// <summary>
        /// Calculates the plot data based on the provided elements and settings.
        /// </summary>
        public static PlotData Calculate(List<PlotElement> elements, PlotSettings settings, int width, int height)
        {
            var initialBounds = CalculateDataBounds(elements);

            // Check for the special case where all visual elements are disabled
            if (!settings.Ticks.Labels.IsVisible &&
                !settings.Ticks.IsVisible &&
                !settings.Axis.IsVisible &&
                !settings.Grid.IsVisible)
            {
                return new PlotData(initialBounds, new Rectangle(0, 0, width, height), new List<Tick>(), new List<Tick>(), new Point(0, 0), elements);
            }

            // Per axis, in priority order: explicit (categorical) ticks (used verbatim, element bounds kept) →
            // a fixed/pinned range (used verbatim, ticks generated within it) → auto (nice-number bounds + ticks).
            // Only the auto path runs the bounds adjustment; the other two keep the axis stable for live updates.
            // Custom ticks and a pinned range are ORTHOGONAL: naming where the ticks go says nothing about how far
            // the axis should reach. A caller that set both used to silently lose the range, because this branch
            // fell back to the data extent — so one stray point (a reference line anchored at 0, say) could stretch
            // the axis far past the pinned window and leave a wide empty margin.
            var (adjustedYBounds, yTicks) = settings.Ticks.CustomYTicks is { Count: > 0 } customY
                ? (settings.FixedYRange is { } pinnedY
                    ? (min: pinnedY.Min, max: pinnedY.Max)
                    : (min: initialBounds.YMin, max: initialBounds.YMax), ToTicks(customY))
                : settings.FixedYRange is { } fixedY
                    ? FixedAxis(settings, fixedY.Min, fixedY.Max, settings.Ticks.DesiredYStep, height)
                    : CalculateAdjustedBoundsAndTicks(settings, initialBounds.YMin,
                        initialBounds.YMax, settings.Ticks.DesiredYStep, height, CalculateXTickLabelSize());
            var (adjustedXBounds, xTicks) = settings.Ticks.CustomXTicks is { Count: > 0 } customX
                ? (settings.FixedXRange is { } pinnedX
                    ? (min: pinnedX.Min, max: pinnedX.Max)
                    : (min: initialBounds.XMin, max: initialBounds.XMax), ToTicks(customX))
                : settings.FixedXRange is { } fixedX
                    ? FixedAxis(settings, fixedX.Min, fixedX.Max, settings.Ticks.DesiredXStep, width)
                    : settings.XWindow is { } xWindow
                        ? FixedAxis(settings, Math.Max(0, initialBounds.XMax - xWindow), initialBounds.XMax, settings.Ticks.DesiredXStep, width)
                        : CalculateAdjustedBoundsAndTicks(settings, initialBounds.XMin,
                            initialBounds.XMax, settings.Ticks.DesiredXStep, width, CalculateYTickLabelSize(yTicks));

            var adjustedBounds = new Bounds(adjustedXBounds.min, adjustedXBounds.max, adjustedYBounds.min,
                adjustedYBounds.max);
            var axisCross = CalculateAxisCross(xTicks, yTicks);
            var drawingArea = CalculateDrawingArea(settings, CalculateXTickLabelSize(), CalculateYTickLabelSize(yTicks),
                width, height);

            return new PlotData(adjustedBounds, drawingArea, xTicks, yTicks, axisCross, elements);
        }

        private static ((double min, double max) bounds, List<Tick> ticks) CalculateAdjustedBoundsAndTicks(
            PlotSettings settings,
            double min,
            double max,
            int desiredStepSize,
            int size,
            int labelSize)
        {
            var tickStep = CalculateTickStep(min, max, desiredStepSize, size);
            // Only the tick VALUES are needed to adjust the bounds; skip building the label strings for this throwaway
            // pass (they're regenerated below over the adjusted range).
            var ticks = GenerateTicks(min, max, tickStep, settings.Ticks.Labels.Format, false, withLabels: false);

            var drawingRange = CalculateDrawingRange(settings, labelSize, size);

            // Adjust the data bounds so that the ticks match the cells
            var (adjustedMin, adjustedMax) = AdjustDataBoundsToTicks(settings, min, max, ticks, drawingRange, labelSize);
            // The new bounds will be wider, so we need to generate new ticks. Build their label strings only when the
            // labels are actually drawn — a hidden-label axis still needs the tick VALUES (axis cross, drawing area)
            // but never the per-tick ToString(format) allocation.
            ticks = GenerateTicks(adjustedMin, adjustedMax, tickStep, settings.Ticks.Labels.Format, true,
                withLabels: settings.Ticks.Labels.IsVisible);

            return ((adjustedMin, adjustedMax), ticks);
        }

        private static int CalculateXTickLabelSize()
        {
            return 1;
        }

        private static int CalculateYTickLabelSize(List<Tick> yTicks)
        {
            var max = 0;
            foreach (var t in yTicks)
                if (t.Label.Length > max) max = t.Label.Length;
            return max;
        }

        private static Bounds CalculateDataBounds(List<PlotElement> elements)
        {
            Bounds? bounds = null;
            foreach (var element in elements)
                bounds = Bounds.Union(bounds, element.GetDataBounds());

            // No finite data anywhere: fall back to a unit box so tick/area math stays well-defined.
            return Pad(bounds ?? new Bounds(0, 1, 0, 1));
        }

        // A zero-width or zero-height data range (a single point, or a flat/constant series) collapses the tick math
        // (NiceNumber(0) yields a zero tick step, and GenerateTicks would then build an invalid range). Pad any
        // degenerate axis to a finite range around its value so the plot is always well-defined and Draw never throws.
        private static Bounds Pad(Bounds b)
        {
            double xMin = b.XMin, xMax = b.XMax, yMin = b.YMin, yMax = b.YMax;
            if (xMax <= xMin) { double p = PadAmount(xMin); xMin -= p; xMax += p; }
            if (yMax <= yMin) { double p = PadAmount(yMin); yMin -= p; yMax += p; }
            return new Bounds(xMin, xMax, yMin, yMax);
        }

        private static double PadAmount(double value)
        {
            double magnitude = Math.Abs(value);
            return magnitude > 0 ? magnitude * 0.5 : 0.5;
        }

        private static double CalculateTickStep(double min, double max, int desiredStep, int size)
        {
            // `size / desiredStep` is INTEGER division, so both operands need guarding before it is used as a
            // divisor. A desiredStep of 0 threw DivideByZeroException outright; a desiredStep wider than the axis
            // floored the quotient to 0, which made the outer division +infinity, NiceNumber return infinity, and
            // GenerateTicks emit a single tick at 0 * infinity = NaN -- a silently broken axis. Clamping to at least
            // one tick degrades an over-wide step to a single tick, which is what "a tick every N cells" should mean
            // once N exceeds the axis length.
            var tickCount = Math.Max(1, size / Math.Max(1, desiredStep));
            return NiceNumber((max - min) / tickCount, true);
        }

        // A pinned axis: the bounds are exactly [min, max] (no adjustment), with nice-number ticks generated inside
        // that range. Keeps the axis stable across live updates instead of tracking the data's changing min/max.
        private static ((double min, double max) bounds, List<Tick> ticks) FixedAxis(
            PlotSettings settings, double min, double max, int desiredStep, int size)
        {
            if (max <= min) max = min + 1;   // guard a degenerate pinned range
            var step = CalculateTickStep(min, max, desiredStep, size);
            // Labels only built when they'll be drawn — the pinned-axis (fixed-range) path is the common live/scope
            // case, so skipping the per-tick ToString each frame when labels are hidden avoids that string churn.
            var withLabels = settings.Ticks.Labels.IsVisible;
            var ticks = GenerateTicks(min, max, step, settings.Ticks.Labels.Format, fitWithinBounds: true, withLabels: withLabels);
            if (ticks.Count == 0)
                ticks.Add(new Tick(min, withLabels ? min.ToString(settings.Ticks.Labels.Format) : string.Empty));
            return ((min, max), ticks);
        }

        private static List<Tick> ToTicks(IReadOnlyList<(double Value, string Label)> ticks)
        {
            var result = new List<Tick>(ticks.Count);
            foreach (var (value, label) in ticks)
                result.Add(new Tick(value, label ?? string.Empty));
            return result;
        }

        private static List<Tick> GenerateTicks(
            double min,
            double max,
            double stepSize,
            string format,
            bool fitWithinBounds,
            bool withLabels = true)
        {
            var minStep = (int)(fitWithinBounds ? Math.Ceiling(min / stepSize) : Math.Round(min / stepSize));
            var maxStep = (int)(fitWithinBounds ? Math.Floor(max / stepSize) : Math.Round(max / stepSize));

            // A plain loop instead of Enumerable.Range(...).Select(...).ToList(): no Range/Select iterators and no
            // captured closure per draw. The List and the per-tick label strings are the only remaining allocations.
            var count = maxStep - minStep + 1;
            var result = new List<Tick>(count > 0 ? count : 0);
            for (var step = minStep; step <= maxStep; step++)
            {
                var tickValue = step * stepSize;
                result.Add(new Tick(tickValue, withLabels ? tickValue.ToString(format) : string.Empty));
            }

            return result;
        }

        private static double NiceNumber(double range, bool round)
        {
            var exponent = Math.Floor(Math.Log10(range));
            var fraction = range / Math.Pow(10, exponent);

            double niceFraction;

            if (round)
            {
                if (fraction < 1.5)
                    niceFraction = 1;
                else if (fraction < 3)
                    niceFraction = 2;
                else if (fraction < 7)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }
            else
            {
                if (fraction <= 1)
                    niceFraction = 1;
                else if (fraction <= 2)
                    niceFraction = 2;
                else if (fraction <= 5)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }

            return niceFraction * Math.Pow(10, exponent);
        }

        /// <summary>
        /// Adjusts the bounds of a graph to fit within a specified number of console characters.
        /// </summary>
        /// <param name="ticks">List of ticks to be displayed on the axis.</param>
        /// <param name="settings">Plot settings</param>
        /// <param name="initialMinValue">Initial minimum value of the data range.</param>
        /// <param name="initialMaxValue">Initial maximum value of the data range.</param>
        /// <param name="totalCharacters">Total number of console characters available for the graph.</param>
        /// <param name="axisLabelWidth">Width of the axis label in characters.</param>
        /// <returns>A tuple containing the adjusted minimum and maximum values for the graph.</returns>
        /// <remarks>
        /// This method ensures that:
        /// <list type="number">
        /// <item><description>All ticks and data points fit within the specified number of characters.</description></item>
        /// <item><description>The axis label (placed to the left of the first tick) doesn't overflow.</description></item>
        /// <item><description>Ticks align with character positions.</description></item>
        /// <item><description>The graph is centered if there's extra space.</description></item>
        /// </list>
        /// The method calculates the optimal character size and adjusts the value range accordingly.
        /// </remarks>
        private static (double min, double max) AdjustDataBoundsToTicks(
            PlotSettings settings,
            double initialMinValue,
            double initialMaxValue,
            List<Tick> ticks,
            int totalCharacters,
            int axisLabelWidth)
        {
            // Find the range of values
            var minTickValue = ticks[0].Value;
            var maxTickValue = ticks[ticks.Count - 1].Value;
            var minValue = Math.Min(initialMinValue, minTickValue);
            var maxValue = Math.Max(initialMaxValue, maxTickValue);

            var cross = CalculateAxisCross(ticks);

            // Calculate character size hypotheses
            var characterSizeByFullRange = (maxValue - minValue) / (totalCharacters - 1);

            // Is the data range starts with the label attached to the axis?
            bool isLabelAtStart;
            double minCharacterSize;

            if (settings.Ticks.Labels.IsVisible && settings.Ticks.Labels.AttachToAxis)
            {
                // Choose the larger character size
                var characterSizeWithLabelAtStart = (maxValue - cross) / (totalCharacters - 1 - axisLabelWidth);
                isLabelAtStart = characterSizeWithLabelAtStart > characterSizeByFullRange;
                minCharacterSize = isLabelAtStart ? characterSizeWithLabelAtStart : characterSizeByFullRange;
            }
            else
            {
                isLabelAtStart = false;
                minCharacterSize = characterSizeByFullRange;
            }

            // Adjust character size based on tick intervals
            var tickInterval = (maxTickValue - minTickValue) / (ticks.Count - 1);
            var charactersPerTick = (int)Math.Floor(tickInterval / minCharacterSize);
            var adjustedCharacterSize = tickInterval / charactersPerTick;

            // Calculate the value of the first character
            var firstCharacterValue = isLabelAtStart
                ? minTickValue - axisLabelWidth * adjustedCharacterSize
                : minTickValue - Math.Ceiling((minTickValue - initialMinValue) / adjustedCharacterSize) *
                adjustedCharacterSize;

            // Center the content if there's extra space
            var usedCharacters = (int)Math.Ceiling((initialMaxValue - firstCharacterValue) / adjustedCharacterSize);
            var unusedCharacters = (totalCharacters - usedCharacters) / 2;
            firstCharacterValue -= unusedCharacters * adjustedCharacterSize;

            // Calculate the value of the last character
            var lastCharacterValue = firstCharacterValue + (totalCharacters - 1) * adjustedCharacterSize;

            return (firstCharacterValue, lastCharacterValue);
        }

        private static Point CalculateAxisCross(List<Tick> xTicks, List<Tick> yTicks)
        {
            return new Point(CalculateAxisCross(xTicks), CalculateAxisCross(yTicks));
        }

        private static double CalculateAxisCross(List<Tick> ticks)
        {
            var min = double.PositiveInfinity;
            foreach (var t in ticks)
            {
                var abs = Math.Abs(t.Value);
                if (abs < min) min = abs;
            }
            return min;
        }

        private static Rectangle CalculateDrawingArea(
            PlotSettings settings,
            int xLabelSize,
            int yLabelSize,
            int width,
            int height)
        {
            var drawingAreaX = CalculateDrawingRange(settings, yLabelSize, width);
            var drawingAreaY = CalculateDrawingRange(settings, xLabelSize, height);

            return new Rectangle(width - drawingAreaX, height - drawingAreaY, drawingAreaX, drawingAreaY);
        }

        private static int CalculateDrawingRange(PlotSettings settings, int labelSize, int size)
        {
            return settings.Ticks.Labels.IsVisible && !settings.Ticks.Labels.AttachToAxis
                ? size - labelSize
                : size;
        }
    }
}