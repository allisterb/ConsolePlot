using System;
using ConsolePlot.Drawing.Tools;

namespace ConsolePlot.Drawing
{
    /// <summary>
    /// A high-resolution (sub-cell) drawing surface over a <see cref="ConsoleImage"/>: a line or point is rasterized
    /// in a virtual grid that is <c>brush.HorizontalResolution</c> × <c>brush.VerticalResolution</c> finer than a
    /// console cell, and the lit sub-cells are folded back into one rich glyph (braille/quadrant) by the pen's brush.
    /// </summary>
    /// <remarks>
    /// A <b>readonly struct</b>, constructed per <see cref="GraphGraphics.DrawLines"/>/<see cref="GraphGraphics.DrawPoints"/>
    /// call on the draw hot path — so it deliberately does NOT inherit the <c>AbstractGraphics</c> machinery (which is a
    /// class, i.e. a per-series heap allocation and a per-point virtual <c>DrawPointCore</c> dispatch). It carries only
    /// the target image, the scaled sub-pixel bounds and the pen, and plots straight through a non-virtual point
    /// writer. The self-contained line clip + Bresenham below are a lean copy of <c>AbstractGraphics</c>'s (that class
    /// stays as-is for <see cref="ConsoleGraphics"/>), specialised to the fixed full-image clip a virtual surface uses.
    /// </remarks>
    public readonly struct VirtualGraphics
    {
        private readonly ConsoleImage _image;
        private readonly PointPen _pen;
        private readonly int _width;   // scaled (sub-pixel) extent = image.Width  × brush.HorizontalResolution
        private readonly int _height;  // scaled (sub-pixel) extent = image.Height × brush.VerticalResolution

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualGraphics"/> struct.
        /// </summary>
        /// <param name="image">The image to draw on.</param>
        /// <param name="pen">The virtual pen to use for drawing.</param>
        public VirtualGraphics(ConsoleImage image, PointPen pen)
        {
            _image = image;
            _pen = pen;
            _width = image.Width * pen.Brush.HorizontalResolution;
            _height = image.Height * pen.Brush.VerticalResolution;
        }

        /// <summary>
        /// Draws a line connecting two points specified by the (sub-pixel) coordinate pairs.
        /// </summary>
        /// <param name="x1">The x-coordinate of the first point.</param>
        /// <param name="y1">The y-coordinate of the first point.</param>
        /// <param name="x2">The x-coordinate of the second point.</param>
        /// <param name="y2">The y-coordinate of the second point.</param>
        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            if (!ClipLine(ref x1, ref y1, ref x2, ref y2))
            {
                return; // Line is completely outside the virtual bounds
            }

            var dx = Math.Abs(x2 - x1);
            var dy = Math.Abs(y2 - y1);
            var sx = x1 < x2 ? 1 : -1;
            var sy = y1 < y2 ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                Plot(x1, y1);
                if (x1 == x2 && y1 == y2)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        /// <summary>
        /// Draws a single point at the specified (sub-pixel) coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate of the point.</param>
        /// <param name="y">The y-coordinate of the point.</param>
        public void DrawPoint(int x, int y) => Plot(x, y);

        // Folds sub-pixel (x, y) into the target cell's rich glyph via the brush. Guards on the REAL buffer bounds
        // (image.Width/Height), not the scaled virtual bounds: line points are already clipped there by ClipLine, but
        // a DrawPoint (scatter) can land off-axis, and a sub-pixel column past the last cell must be dropped rather
        // than written out of the image. DivRem gives a negative bufferX/bufferY for a negative coordinate, which the
        // same bounds test rejects.
        private void Plot(int x, int y)
        {
            var bufferX = Math.DivRem(x, _pen.Brush.HorizontalResolution, out var subX);
            var bufferY = Math.DivRem(y, _pen.Brush.VerticalResolution, out var subY);

            if (bufferX < 0 || bufferX >= _image.Width || bufferY < 0 || bufferY >= _image.Height)
                return;

            var cell = _image.GetCharacter(bufferX, bufferY);
            var oldChar = cell.Foreground == _pen.Color ? cell.Content ?? ' ' : ' ';
            var newChar = _pen.Brush.RenderPoint(oldChar, subX, subY);
            _image.SetPixel(bufferX, bufferY, newChar, _pen.Color);
        }

        // Cohen–Sutherland clip against the fixed full virtual rectangle [0, _width-1] × [0, _height-1].
        private bool ClipLine(ref int x1, ref int y1, ref int x2, ref int y2)
        {
            const int INSIDE = 0; // 0000
            const int LEFT = 1;   // 0001
            const int RIGHT = 2;  // 0010
            const int BOTTOM = 4; // 0100
            const int TOP = 8;    // 1000

            int left = 0, bottom = 0, right = _width - 1, top = _height - 1;

            int ComputeOutCode(int x, int y)
            {
                var code = INSIDE;
                if (x < left)
                {
                    code |= LEFT;
                }
                else if (x > right)
                {
                    code |= RIGHT;
                }

                if (y < bottom)
                {
                    code |= BOTTOM;
                }
                else if (y > top)
                {
                    code |= TOP;
                }

                return code;
            }

            var outcode1 = ComputeOutCode(x1, y1);
            var outcode2 = ComputeOutCode(x2, y2);
            var accept = false;

            while (true)
            {
                if ((outcode1 | outcode2) == 0)
                {
                    accept = true;
                    break;
                }

                if ((outcode1 & outcode2) != 0)
                {
                    break;
                }

                int x = 0, y = 0;
                var outcodeOut = outcode1 != 0 ? outcode1 : outcode2;

                if ((outcodeOut & TOP) != 0)
                {
                    x = x1 + (x2 - x1) * (top - y1) / (y2 - y1);
                    y = top;
                }
                else if ((outcodeOut & BOTTOM) != 0)
                {
                    x = x1 + (x2 - x1) * (bottom - y1) / (y2 - y1);
                    y = bottom;
                }
                else if ((outcodeOut & RIGHT) != 0)
                {
                    y = y1 + (y2 - y1) * (right - x1) / (x2 - x1);
                    x = right;
                }
                else if ((outcodeOut & LEFT) != 0)
                {
                    y = y1 + (y2 - y1) * (left - x1) / (x2 - x1);
                    x = left;
                }

                if (outcodeOut == outcode1)
                {
                    x1 = x;
                    y1 = y;
                    outcode1 = ComputeOutCode(x1, y1);
                }
                else
                {
                    x2 = x;
                    y2 = y;
                    outcode2 = ComputeOutCode(x2, y2);
                }
            }

            return accept;
        }
    }
}
