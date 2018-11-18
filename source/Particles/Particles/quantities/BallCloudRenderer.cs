using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace Particles
{
    /// <summary>
    /// A simple renderer for ball clouds.
    /// </summary>
    public class BallCloudRenderer : IRenderer<BallCloud, Task<Image>>, IDisposable
    {
        private readonly int cameraZ;
        private readonly int B;
        private readonly double scale;

        private bool busy = false;
        private Vector3[] positions;
        private double[] radii;
        private readonly Bitmap[] bitmaps;
        private readonly Graphics[] graphics;
        private readonly Task[] tasks;

        public BallCloudRenderer(int width, int height, double scale)
        {
            this.scale = scale;
            this.cameraZ = Math.Max(width, height);
            this.B = cameraZ * cameraZ / 2; // A constant factor chosen such that that z = 0 gives brightness 0.5

            var pc = Environment.ProcessorCount;

            this.bitmaps = new Bitmap[pc];
            this.graphics = new Graphics[pc];
            this.tasks = new Task[pc];
            for (var i = 0; i < pc; i++){
                var bmp = new Bitmap(width, height);
                bmp.MakeTransparent(Color.Black);
                var g = Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                bitmaps[i] = bmp;
                graphics[i] = g;
            }
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
                foreach (var g in graphics)
                    g.Dispose();
        }

        private class ZComparer : IComparer<Vector3>
        {
            public static ZComparer Instance = new ZComparer();
            public int Compare(Vector3 x, Vector3 y)
            {
                return x.Z.CompareTo(y.Z);
            }
        }

        private async Task render(int tid, int startIndex, int count)
        {
            var bmp = bitmaps[tid];
            var g = graphics[tid];

            g.Clear(Color.Black); // Black is transparency for bmp.

            for (int i = Math.Max(0, startIndex); i < Math.Min(startIndex + count, positions.Length); i++)
            {
                var p = scale * positions[i];
                var r = Math.Max(1, (int)(scale * radii[i]));
                var x = bmp.Width / 2 + (int)(p.X);
                var y = bmp.Height / 2 - (int)(p.Y);

                var d = cameraZ - p.Z;
                var b = Math.Min(255, (int)(255 * B / (d * d)));

                g.FillEllipse(GetBrush(b), x - r / 2, y - r / 2, r, r);
            }
            g.Flush(FlushIntention.Sync);

            var stride = 2;

            while (tid % stride == 0 && tid + stride / 2 < tasks.Length)
            {
                await tasks[tid + stride / 2];
                g.DrawImageUnscaled(bitmaps[tid + stride / 2], 0, 0);
                g.Flush(FlushIntention.Sync);
                stride *= 2;
            }
        }

        public async Task<Image> Render(BallCloud c)
        {
            lock (this)
            {
                if (busy)
                    throw new InvalidOperationException(string.Format("The renderer is already occupied with a call to {0}! Do not call {0} before the previous call completed!", nameof(Render)));
                busy = true;
            }

            var N = c.Positions.Length;

            // Sort particles by Z:
            var ps = new Vector3[N];
            var rs = new double[N];
            Array.Copy(c.Positions, ps, N);
            Array.Copy(c.Radii, rs, N);
            Array.Sort(ps, rs, ZComparer.Instance);

            // Launch tasks:
            var pc = Environment.ProcessorCount;

            var bpp = Math.Max(64, N / pc);
            var abpp = N % bpp;

            var count = bpp + abpp;

            int tid = 0;
            for (int i = N - count; i >= 0; i -= count)
            {
                tasks[i] = render(tid--, i, count);
                count = bpp;
            }

            await tasks[0];

            lock (this)
            {
                busy = false;
                return (Image)bitmaps[0].Clone();
            }
        }
    }
}
