using System;
using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// A heatmap: a 2D grid of values (rows × cols, row 0 at the top) tiled over a data rectangle, each cell
    /// coloured by mapping its value — normalised into [<see cref="VMin"/>, <see cref="VMax"/>] — through
    /// <see cref="ColorMap"/>. Used for heatmaps, matrix plots and confusion matrices (change B).
    /// </summary>
    public class HeatSeries : PlotElement
    {
        /// <summary>The grid values, one list per row (row 0 is drawn at the top).</summary>
        public IReadOnlyList<IReadOnlyList<double>> Values { get; }

        /// <summary>Left/right of the data rectangle the grid tiles over.</summary>
        public double XMin { get; }
        public double XMax { get; }

        /// <summary>Bottom/top of the data rectangle the grid tiles over.</summary>
        public double YMin { get; }
        public double YMax { get; }

        /// <summary>Value mapped to the low end of the colour map.</summary>
        public double VMin { get; }

        /// <summary>Value mapped to the high end of the colour map.</summary>
        public double VMax { get; }

        /// <summary>Maps a normalised value in [0, 1] to a cell colour.</summary>
        public Func<double, ConsoleGUI.Data.Color> ColorMap { get; }

        /// <summary>Formats a cell's value as text drawn centred in the cell, or <see langword="null"/> for no text.</summary>
        public Func<double, string> CellText { get; }

        public HeatSeries(
            IEnumerable<IEnumerable<double>> values, double xMin, double xMax, double yMin, double yMax,
            double vmin, double vmax, Func<double, ConsoleGUI.Data.Color> colorMap, Func<double, string> cellText = null)
        {
            var grid = new List<IReadOnlyList<double>>();
            foreach (var row in values) grid.Add(new List<double>(row));
            Values = grid;

            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            VMin = vmin;
            VMax = vmax;
            ColorMap = colorMap ?? throw new ArgumentNullException(nameof(colorMap));
            CellText = cellText;
        }

        internal override Bounds GetDataBounds() =>
            Values.Count == 0 ? null : new Bounds(XMin, XMax, YMin, YMax);

        internal override void Draw(GraphGraphics graphics) =>
            graphics.DrawHeat(Values, XMin, XMax, YMin, YMax, VMin, VMax, ColorMap, CellText);
    }
}
