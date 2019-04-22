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
            if (size.X < 0 || size.Y < 0 || size.Z < 0)
            {
                var a = origin;
                var b = origin + size;
                origin = new Vector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
                size = new Vector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)) - origin;
            }
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

        /// <summary>
        /// Computes the intersection of two AABB's.
        /// </summary>
        /// <param name="a">An AABB.</param>
        /// <param name="b">An AABB.</param>
        public static AABB Intersect(AABB a, AABB b)
        {
            var startA = a.Origin;
            var startB = b.Origin;
            var endA = a.Origin + a.Size;
            var endB = b.Origin + b.Size;

            var ox = Math.Max(startA.X, startB.X);
            var sx = Math.Max(0, Math.Min(endA.X, endB.X) - ox);
            var oy = Math.Max(startA.Y, startB.Y);
            var sy = Math.Max(0, Math.Min(endA.Y, endB.Y) - oy);
            var oz = Math.Max(startA.Z, startB.Z);
            var sz = Math.Max(0, Math.Min(endA.Z, endB.Z) - oz);

            return new AABB(new Vector3(ox, oz, oz), new Vector3(sx, sy, sz));
        }

        public bool Equals(AABB other)
        {
            if (this.IsEmpty) return other.IsEmpty;
            if (other.IsEmpty) return false;
            return this.origin.Equals(other.origin) && this.size.Equals(other.size);
        }

        public override bool Equals(object obj)
        {
            return obj is AABB && this.Equals((AABB)obj);
        }

        public override int GetHashCode()
        {
            return (size.X * size.Y * size.Z).GetHashCode();
        }

        public static bool operator ==(AABB a, AABB b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(AABB a, AABB b)
        {
            return !a.Equals(b);
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
        /// <remarks>
        /// All the components of this vector are guaranteed to be nonnegative!
        /// </remarks>
        public Vector3 Size
        {
            get
            {
                return size;
            }
        }

        /// <summary>
        /// Indicates if this AABB is empty, i.e. has a volume of zero.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return size.X < double.Epsilon || size.Y < double.Epsilon || size.Z < double.Epsilon;
            }
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}x{1}x{2}@{3}", origin.X, origin.Y, origin.Z, size);
        }
    }
}
