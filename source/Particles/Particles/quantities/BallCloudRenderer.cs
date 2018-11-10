using System;
using System.Threading.Tasks;
using System.Drawing;

namespace Particles
{
    /// <summary>
    /// A simple renderer for ball clouds.
    /// </summary>
    public class BallCloudRenderer : IRenderer<BallCloud, Image>
    {
        private readonly Bitmap bmp;
        private readonly int cameraZ;
        private readonly int B;
        private readonly double scale;

        public BallCloudRenderer(int width, int height, double scale)
        {
            this.scale = scale;
            this.bmp = new Bitmap(width, height);
            this.cameraZ = Math.Max(width, height);
            this.B = cameraZ * cameraZ / 2; // A constant factor chosen such that that z = 0 gives brightness 0.5
        }

        public Image Render(BallCloud c)
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.Clear(Color.Black);

                for (int i = 0; i < c.Positions.Length; i++)
                {
                    var p = scale * c.Positions[i];
                    var r = Math.Max(1, (int)(scale * c.Radii[i]));
                    var x = bmp.Width / 2 + (int)(p.X);
                    var y = bmp.Height / 2 - (int)(p.Y);

                    var d = cameraZ - p.Z;
                    var b = Math.Min(255, (int)(255 * B / (d * d)));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(192, b, b, b)), x - r / 2, y - r / 2, r, r);
                }
            }

            return (Image)bmp.Clone();
        }
    }
}
