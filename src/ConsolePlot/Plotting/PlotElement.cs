using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Base class for a drawable plot element — a series, scatter, stem, bars, candles, and so on. Each element
    /// reports its data bounds (so the plot can scale the axes to fit every element) and knows how to draw itself in
    /// the plot's data coordinate system. New plot types are added by deriving from this class; the shared axes,
    /// grid, ticks and coordinate conversion are provided by the surrounding <see cref="Plot"/>.
    /// </summary>
    public abstract class PlotElement
    {
        /// <summary>The data-space bounds this element occupies, or <see langword="null"/> when it has no finite data.</summary>
        internal abstract Bounds? GetDataBounds();

        /// <summary>Draws the element using data coordinates (the graphics context converts to the drawing area).</summary>
        internal abstract void Draw(GraphGraphics graphics);

        /// <summary>
        /// Adopts <paramref name="values"/> as a random-access list without copying when it already is one (an array
        /// or <see cref="List{T}"/>), else materializes it once. Live series are rebuilt from the same backing list
        /// every frame, so this avoids a per-frame copy of every data point on the render path. The element then holds
        /// a <em>reference</em> to the caller's list; callers that keep mutating it (e.g. Jumbee's live series) rebuild
        /// the element per update, so each draw still sees a stable snapshot.
        /// </summary>
        protected static IReadOnlyList<double> AsList(IEnumerable<double> values) =>
            values as IReadOnlyList<double> ?? new List<double>(values);
    }
}
