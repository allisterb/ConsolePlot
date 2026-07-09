using System.Collections.Generic;

namespace ConsolePlot.Plotting
{
    internal class Bounds
    {
        public double XMin { get; }
        public double XMax { get; }
        public double YMin { get; }
        public double YMax { get; }

        public Bounds(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        /// <summary>Bounds enclosing the finite points of the paired series, or <see langword="null"/> if none are finite.</summary>
        public static Bounds FromXY(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            bool any = false;
            double xMin = double.PositiveInfinity, xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
            for (int i = 0; i < xs.Count; i++)
            {
                double x = xs[i], y = ys[i];
                if (double.IsInfinity(x) || double.IsNaN(x) || double.IsInfinity(y) || double.IsNaN(y))
                    continue;
                any = true;
                if (x < xMin) xMin = x;
                if (x > xMax) xMax = x;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }

            return any ? new Bounds(xMin, xMax, yMin, yMax) : null;
        }

        /// <summary>Extends the bounds vertically to include <paramref name="y"/> (e.g. a stem/bar baseline).</summary>
        public Bounds IncludeY(double y) =>
            new Bounds(XMin, XMax, System.Math.Min(YMin, y), System.Math.Max(YMax, y));

        /// <summary>Extends the bounds horizontally to include <paramref name="x"/> (e.g. a horizontal-bar baseline).</summary>
        public Bounds IncludeX(double x) =>
            new Bounds(System.Math.Min(XMin, x), System.Math.Max(XMax, x), YMin, YMax);

        /// <summary>The smallest bounds enclosing both <paramref name="a"/> and <paramref name="b"/> (either may be null).</summary>
        public static Bounds Union(Bounds a, Bounds b)
        {
            if (a is null) return b;
            if (b is null) return a;
            return new Bounds(
                System.Math.Min(a.XMin, b.XMin), System.Math.Max(a.XMax, b.XMax),
                System.Math.Min(a.YMin, b.YMin), System.Math.Max(a.YMax, b.YMax));
        }
    }
}
