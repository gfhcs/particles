using System;
using System.Collections.Generic;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB).
    /// </summary>
    public struct AABB : IEquatable<AABB>
    {
        private readonly Vector3 origin, size;

        /// <summary>
        /// Creates a new axis-aligned bounding box
        /// </summary>
        /// <param name="origin">The origin of the box, i.e. the point with the minimal coordinates for all dimensions.</param>
        /// <param name="size">The size of the bounding box in all dimensions.</param>
        public AABB(Vector3 origin, Vector3 size)
        {
            this.origin = origin;
            this.size = size;
        }

        /// <summary>
        /// Creates a new axis-aligned bounding box of size 0.
        /// </summary>
        /// <param name="origin">The origin of the box, i.e. the point with the minimal coordinates for all dimensions.</param>
        public AABB(Vector3 origin) : this(origin, new Vector3(0, 0, 0))
        {
        }

        /// <summary>
        /// Returns the smallest AABB that contains the union of two given AABB.
        /// </summary>
        /// <param name="a">An AABB.</param>
        /// <param name="b">An AABB.</param>
        public static AABB Bound(AABB a, AABB b)
        {
            var oa = a.Origin;
            var ea = oa + a.Size;
            var ob = b.Origin;
            var eb = ob + b.Size;

            var on = new Vector3(Math.Min(oa.X, ob.X), Math.Min(oa.Y, ob.Y), Math.Min(oa.Z, ob.Z));
            var sn = new Vector3(Math.Max(ea.X, eb.X), Math.Max(ea.Y, eb.Y), Math.Max(ea.Z, eb.Z)) - on;

            return new AABB(on, sn);
        }

        public bool Equals(AABB other)
        {
            return this.origin.Equals(other.origin) && this.size.Equals(other.size);
        }

        /// <summary>
        /// The origin of the box, i.e. the point with the minimal coordinates for all dimensions.
        /// </summary>
        public Vector3 Origin
        {
            get
            {
                return origin;
            }
        }

        /// <summary>
        /// The size of the bounding box in all dimensions.
        /// </summary>
        public Vector3 Size
        {
            get
            {
                return size;
            }
        }
    }
}
