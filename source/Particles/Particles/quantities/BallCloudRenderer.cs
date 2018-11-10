using System;
using System.Threading.Tasks;
using System.Drawing;

namespace Particles
{
    /// <summary>
    /// A simple renderer for ball clouds.
    /// </summary>
    public class BallCloudRenderer : IRenderer<BallCloud, Image>, IDisposable
    {
        private readonly Bitmap bmp;
        private readonly Graphics g;
        private readonly int cameraZ;
        private readonly int B;
        private readonly double scale;

        public BallCloudRenderer(int width, int height, double scale)
        {
            this.scale = scale;
            this.cameraZ = Math.Max(width, height);
            this.B = cameraZ * cameraZ / 2; // A constant factor chosen such that that z = 0 gives brightness 0.5

            this.bmp = new Bitmap(width, height);
            this.g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        }

        private static SolidBrush[] brushCache = new SolidBrush[256];
        public static SolidBrush GetBrush(int brightness)
        {
            lock (brushCache)
            {
                var brush = brushCache[brightness];
                if (brush == null){
                    var b = Math.Min(255, brightness);

                    brush = new SolidBrush(Color.FromArgb(255, brightness, brightness, brightness));
                    brushCache[brightness] = brush;
                }
                return brush;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                g.Dispose();
        }

        public Image Render(BallCloud c)
        {
            g.Clear(Color.Black);

            for (int i = 0; i < c.Positions.Length; i++)
            {
                var p = scale * c.Positions[i];
                var r = Math.Max(1, (int)(scale * c.Radii[i]));
                var x = bmp.Width / 2 + (int)(p.X);
                var y = bmp.Height / 2 - (int)(p.Y);

                var d = cameraZ - p.Z;
                var b = Math.Min(255, (int)(255 * B / (d * d)));

                g.FillEllipse(GetBrush(b), x - r / 2, y - r / 2, r, r);
            }

            return (Image)bmp.Clone();
        }
    }
}
