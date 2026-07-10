namespace ConsolePlot.Plotting
{
    /// <summary>
    /// Converts coordinates between different coordinate systems. A value type: one is created per draw and one per
    /// series (the scaled sub-cell converter), so a struct keeps them off the GC heap.
    /// </summary>
    internal readonly struct CoordinateConverter
    {
        public (double Min, double Max) SourceX { get; }
        public (double Min, double Max) SourceY { get; }
        public (int Min, int Max) TargetX { get; }
        public (int Min, int Max) TargetY { get; }

        public CoordinateConverter(
            double sourceXMin, double sourceXMax, int targetXMin, int targetXMax,
            double sourceYMin, double sourceYMax, int targetYMin, int targetYMax)
        {
            SourceX = (sourceXMin, sourceXMax);
            SourceY = (sourceYMin, sourceYMax);
            TargetX = (targetXMin, targetXMax);
            TargetY = (targetYMin, targetYMax);
        }

        public double ConvertX(double value) =>
            ConvertValue(value, SourceX.Min, SourceX.Max, TargetX.Min, TargetX.Max);

        public double ConvertY(double value) =>
            ConvertValue(value, SourceY.Min, SourceY.Max, TargetY.Min, TargetY.Max);

        public (double X, double Y) Convert(double x, double y) => (ConvertX(x), ConvertY(y));

        private static double ConvertValue(double value, double sourceMin, double sourceMax, int targetMin, int targetMax) =>
            (value - sourceMin) / (sourceMax - sourceMin) * (targetMax - targetMin) + targetMin;
    }
}