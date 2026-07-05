namespace ConsolePlot.Plotting
{
    /// <summary>Horizontal anchoring of a label relative to its point.</summary>
    public enum LabelAlignment
    {
        /// <summary>The text starts at the point and runs right.</summary>
        Left,
        /// <summary>The text is centred on the point.</summary>
        Center,
        /// <summary>The text ends at the point.</summary>
        Right,
    }

    /// <summary>
    /// A text annotation anchored to a data coordinate: draws <see cref="Text"/> near the point (<see cref="X"/>,
    /// <see cref="Y"/>) with a full-colour foreground and optional background. Offsets nudge it in cells and
    /// <see cref="Alignment"/> anchors it horizontally. An annotation does not affect the plot's data bounds, so it
    /// never rescales the axes.
    /// </summary>
    public class PointLabel : PlotElement
    {
        public double X { get; }
        public double Y { get; }
        public string Text { get; }
        public ConsoleGUI.Data.Color Foreground { get; }
        public ConsoleGUI.Data.Color? Background { get; }
        public LabelAlignment Alignment { get; }

        /// <summary>Horizontal offset from the point, in cells (positive = right).</summary>
        public int OffsetX { get; }

        /// <summary>Vertical offset from the point, in cells (positive = up / above the point).</summary>
        public int OffsetY { get; }

        public PointLabel(
            double x, double y, string text,
            ConsoleGUI.Data.Color foreground, ConsoleGUI.Data.Color? background = null,
            LabelAlignment alignment = LabelAlignment.Center, int offsetX = 0, int offsetY = 1)
        {
            X = x;
            Y = y;
            Text = text ?? string.Empty;
            Foreground = foreground;
            Background = background;
            Alignment = alignment;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        // Annotations ride on top of existing data; they must not expand the axes.
        internal override Bounds GetDataBounds() => null;

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawLabel(X, Y, Text, Foreground, Background, Alignment, OffsetX, OffsetY);
    }
}
