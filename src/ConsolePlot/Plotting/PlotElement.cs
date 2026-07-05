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
        internal abstract Bounds GetDataBounds();

        /// <summary>Draws the element using data coordinates (the graphics context converts to the drawing area).</summary>
        internal abstract void Draw(GraphGraphics graphics);
    }
}
