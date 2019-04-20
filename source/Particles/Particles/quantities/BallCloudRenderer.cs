using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// Renders states of a ball cloud to images.
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
        private Task<Image> resultTask = null;
        private readonly Graphics[] graphics;
        private readonly Task[] tasks;

        private readonly Color transparent = Color.FromArgb(0, 0, 0, 0);

        /// <summary>
        /// Creates a new renderer for ball clouds.
        /// </summary>
        /// <param name="width">The frame width of the rendered images.</param>
        /// <param name="height">The frame height of the rendered images.</param>
        /// <param name="scale">The factor by which lengths in world space are to be multiplied to obtain image space coordinates (pixels per meter).</param>
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
                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var g = Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceOver;
                bitmaps[i] = bmp;
                graphics[i] = g;
            }
        }

        /// <summary>
        /// The frame width of the rendered images.
        /// </summary>
        /// <value>The width.</value>
        public int Width
        {
            get { return bitmaps[0].Width; }
        }

        /// <summary>
        /// The frame width of the rendered images.
        /// </summary>
        /// <value>The width.</value>
        public int Height
        {
            get { return bitmaps[0].Height; }
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
            {
                lock (this)
                {
                    if (resultTask != null)
                        resultTask.Wait();
                    foreach (var g in graphics)
                        g.Dispose();
                    foreach (var bmp in bitmaps)
                        bmp.Dispose();
                }
            }
        }

        /// <summary>
        /// Maps the given point (in world coordinates) to image space.
        /// </summary>
        /// <param name="worldPoint">A position in 3D space.</param>
        protected Point ToImage(Vector3 worldPoint)
        {
            var p = scale * worldPoint;
            var x = this.Width / 2 + (int)(p.X);
            var y = this.Height / 2 - (int)(p.Y);

            return new Point(x, y);
        }

        private class ZComparer : IComparer<Vector3>
        {
            public static ZComparer Instance = new ZComparer();
            public int Compare(Vector3 x, Vector3 y)
            {
                return x.Z.CompareTo(y.Z);
            }
        }

        private Task render(int nc, int tid, int startIndex, int count)
        {
            return Task.Run(async () =>
            {
                var bmp = bitmaps[tid];
                var g = graphics[tid];

                g.Clear(tid == 0 ? Color.Black : transparent);

                for (int i = startIndex; i < startIndex + count; i++)
                {
                    var p = this.ToImage(positions[i]);

                    var r = Math.Max(1, (int)(scale * radii[i]));

                    var d = cameraZ - scale * positions[i].Z;
                    var b = Math.Max(0, Math.Min(255, (int)(255 * B / (d * d))));

                    g.FillEllipse(GetBrush(b), p.X - r / 2, p.Y - r / 2, r, r);
                }
                g.Flush(FlushIntention.Sync);

                var stride = 2;

                while (tid % stride == 0 && tid + stride / 2 < nc)
                {
                    await tasks[tid + stride / 2].ConfigureAwait(false);
                    g.DrawImageUnscaled(bitmaps[tid + stride / 2], 0, 0);
                    g.Flush(FlushIntention.Sync);
                    stride *= 2;
                }
            });
        }

        public void Render(BallCloud c)
        {
            lock (this)
            {
                if (busy)
                    throw new InvalidOperationException(string.Format("The renderer is already occupied with a call to {0}! Do not call {0} before the previous call completed!", nameof(Render)));
                busy = true;

                this.resultTask = Task.Run(async () =>
                {
                    var N = c.Positions.Length;

                    // Sort particles by Z:
                    if (positions == null || positions.Length != N)
                    {
                        positions = new Vector3[N];
                        radii = new double[N];
                    }
                    Array.Copy(c.Positions, positions, N);
                    Array.Copy(c.Radii, radii, N);
                    Array.Sort(positions, radii, ZComparer.Instance);

                    // Launch tasks:
                    var pc = Environment.ProcessorCount;

                    var bpp = Math.Min(N, Math.Max(256, N / pc));

                    var tc = bpp > 0 ? N / bpp : 1;

                    var count = bpp + N % bpp;
                    var i = N - count;

                    for (int tid = tc - 1; tid >= 0; tid--)
                    {
                        tasks[tid] = render(tc, tid, i, count);
                        count = bpp;
                        i -= bpp;
                    }

                    await Task.WhenAll(tasks.Take(tc)).ConfigureAwait(false);

                    lock (this)
                    {
                        busy = false;
                        return (Image)bitmaps[0];
                    }
                });
            }
        }

        /// <summary>
        /// The result of the last call to Render.
        /// The object returned by this task is not thread-safe and must not be accessed as long as this property holds an unfinished task!
        /// </summary>
        /// <remarks>
        /// The object returned by this task is owned and managed by the <see cref="BallCloudRenderer"/>, which means that calls to methods
        /// of <see cref="BallCloudRenderer"/> or even disposing the instance that created this task may make the image unusable (because
        /// it is concurrently written to or deallocated). This is why users of this property should only work on *copies* of the obtained image,
        /// should they intend to keep accessing it after the next call to <see cref="Render(BallCloud)"/> or after disposing the <see cref="BallCloudRenderer"/>.
        /// </remarks>
        public Task<Image> RenderedState
        {
            get
            {
                return resultTask;
            }
        }
    }
}
