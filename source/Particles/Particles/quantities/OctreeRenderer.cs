using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Collections;

namespace Particles
{
    /// <summary>
    /// A renderer that visualized an octree partitioning the space around a ball cloud.
    /// </summary>
    public class OctreeRenderer : BallCloudRenderer
    {
        private (int, Vector3)[] positionIndex;
        private Task<Image> task;
        private readonly Bitmap bmp;
        private readonly Graphics g;
        private bool busy = false;

        private static readonly Pen octreePen = Pens.Green;

        /// <summary>
        /// Creates a new octree renderer.
        /// </summary>
        /// <param name="width">The frame width of the rendered images.</param>
        /// <param name="height">The frame height of the rendered images.</param>
        /// <param name="scale">The factor by which lengths in world space are to be multiplied to obtain image space coordinates (pixels per meter).</param>
        public OctreeRenderer(int width, int height, double scale) : base(width, height, scale)
        {
            bmp = new Bitmap(width, height);
            g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.CompositingMode = CompositingMode.SourceOver;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (this)
                {
                    if (task != null)
                        task.Wait();
                        g.Dispose();
                        bmp.Dispose();
                        
                    base.Dispose(disposing);
                }
            }
        }

        /// <summary>
        /// Renders the given ball cloud, together with an octree partitioning it. The result can be obtained via <see cref="RenderedState"/>.
        /// This method assumes that the given quantity evolved from the quantity
        /// it was last called for.
        /// </summary>
        /// <param name="c">The ball cloud to render.</param>
        public override void Render(BallCloud c)
        {
            this.Render(c, null);
        }

        /// <summary>
        /// Renders the given ball cloud, together with an octree partitioning it. The result can be obtained via <see cref="RenderedState"/>.
        /// This method assumes that the given quantity evolved from the quantity
        /// it was last called for.
        /// </summary>
        /// <param name="c">The ball cloud to render.</param>
        /// <param name="mask">Indicates which cells of the octree should be visualized: A cell is visualized only, if the mask enumerates <see cref="true"/> for
        /// one of the particles it contains.</param>
        public void Render(BallCloud c, bool[] mask)
        {
            AABB bnd(int idx)
            {
                var p = c.Positions[idx];
                var r = c.Radii[idx];
                var rv = new Vector3(r, r, r);
                return new AABB(p - rv, 2 * rv);
            }

            // Renders the given AABB.
            void draw(AABB box)
            {
                // We ignore the Z dimension, because it's not visible in this kind of renderer anyway.

                var o = ToImage(box.Origin);
                var s = ScaleDelta(box.Size);

                g.DrawRectangle(octreePen, o.X, o.Y + s.Item2, s.Item1, -s.Item2);
            }

            // Render the octree:
            (bool, AABB) render(MortonOctree<int>.INodeReference node)
            {
                AABB bb;
                bool m;

                if (node.IsLeaf)
                {
                    bb = AABB.Bound(from item in node.Items select bnd(item.Item1));
                    m = mask == null || node.Items.Any((idx) => mask[idx.Item1]);
                }
                else
                {
                    bb = AABB.Empty;
                    m = false;
                    foreach (var child in node.Children)
                    {
                        var (cm, cb) = render(child);
                        bb = AABB.Bound(bb, cb);
                        m |= cm;
                    }

                }

                if (m)
                    draw(bb);
                return (m, bb);
            }

            // Launch rendering of the balls:
            base.Render(c);

            lock (this)
            {
                if (busy)
                    throw new InvalidOperationException(string.Format("The renderer is already occupied with a call to {0}! Do not call {0} before the previous call completed!", nameof(Render)));
                busy = true;

                this.task = Task.Run(async () =>
                {
                    // Create the octree:
                    var N = c.Positions.Length;

                    if (positionIndex == null || positionIndex.Length < N)
                        positionIndex = new (int, Vector3)[N];

                    var bounds = bnd(0);
                    for (int i = 0; i < N; i++)
                    {
                        positionIndex[i] = (i, c.Positions[i]);
                        bounds = AABB.Bound(bounds, bnd(i));
                    }
                    var tree = new MortonOctree<int>(positionIndex, bounds);

                    // Render the octree:
                    g.Clear(Transparent);
                    render(tree.Root);

                    // Overlay the octree on the rendered cloud:
                    var underlying = await base.RenderedState;

                    lock (this) { 
                        using (var g2 = Graphics.FromImage(underlying))
                        {
                            g2.DrawImageUnscaled(bmp, 0, 0);
                            g2.Flush(FlushIntention.Sync);
                        }

                        busy = false;
                        return underlying;
                    }
                });
            }
        }

        public override Task<Image> RenderedState
        {
            get
            {
                return task;
            }
        }
    }
}
