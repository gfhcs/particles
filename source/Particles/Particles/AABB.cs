using System;
using System.Collections.Generic;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB).
    /// Axis-aligned boxes can be empty, contain a single point, or more than one point.
    /// Empty AABB's have an origin of <see cref="Vector3.NaV"/>.
    /// AABB's that contain only one single point have a size of 0.
    /// </summary>
    public struct AABB : IEquatable<AABB>
    {
        private readonly Vector3 origin, size;

        /// <summary>
        /// An empty AABB.
        /// </summary>
        public static readonly AABB Empty = new AABB(Vector3.NaV, new Vector3());

        /// <summary>
        /// An AABB that contains all the points of 3D space.
        /// </summary>
        public static readonly AABB Full = new AABB(new Vector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity), new Vector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity));


        /// <summary>
        /// Creates a new axis-aligned bounding box.
        /// </summary>
        /// <remarks>
        /// To create an empty box, give <see cref="Vector3.NaV"/> as origin
        /// and a zero vector as size.
        /// For a singularity AABB, origin should be a proper point, while size is a zero vector.
        /// </remarks>
        /// <param name="origin">The origin of the box, i.e. the point with the minimal coordinates for all dimensions.</param>
        /// <param name="size">The size of the bounding box in all dimensions.</param>
        public AABB(Vector3 origin, Vector3 size)
        {
            if (Vector3.IsNaV(origin))
            {
                if (size != new Vector3())
                    throw new ArgumentException(string.Format("Passing {0} as origin requires a zero vector as size!", nameof(Vector3.NaV)), nameof(size));
                origin = Vector3.NaV; // Make sure that *all* components of origin are NaN! Important for equality, comparison and intersection!
            }
            else if (size.X < 0 || size.Y < 0 || size.Z < 0)
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
        /// <remarks>Even though the size of the new box is 0, the box is not empty, but contains the given point
        /// as its only element!</remarks>
        /// <param name="origin">The origin of the box, i.e. the point with the minimal coordinates for all dimensions.</param>
        public AABB(Vector3 origin) : this(origin, new Vector3(0, 0, 0))
        {
        }

        /// <summary>
        /// Returns the smallest AABB that contains all the given points.
        /// </summary>
        /// <param name="points">A set of points.</param>
        public static AABB Bound (IEnumerable<Vector3> points)
        {
            using (var g = points.GetEnumerator())
            {
                if (g.MoveNext())
                {
                    var o = g.Current;
                    var e = o;

                    while (g.MoveNext())
                    {
                        var p = g.Current;
                        o = new Vector3(Math.Min(o.X, p.X), Math.Min(o.Y, p.Y), Math.Min(o.Z, p.Z));
                        e = new Vector3(Math.Max(o.X, p.X), Math.Max(o.Y, p.Y), Math.Max(o.Z, p.Z));
                    }

                    if (Vector3.IsNaV(o))
                        throw new ArgumentException("An underspecified point was given, i.e. with at least one coordinate being NaN!");

                    return new AABB(o, e - o);
                }
                return AABB.Empty;
            }
        }

        /// <summary>
        /// Returns the smallest AABB that contains all the given points.
        /// </summary>
        /// <param name="points">A set of points.</param>
        public static AABB Bound(params Vector3[] points)
        {
            return Bound((IEnumerable<Vector3>)points);
        }

        /// <summary>
        /// Returns the smallest AABB that contains the union of the given AABB's.
        /// </summary>
        /// <param name="bbs">A set of AABB's.</param>
        public static AABB Bound(IEnumerable<AABB> bbs)
        {
            IEnumerable<Vector3> traverse()
            {
                foreach (var b in bbs)
                    if (!b.IsEmpty) {
                        yield return b.Origin;
                        yield return b.Origin + b.Size;
                    }
            }

            return Bound(traverse());
        }

        /// <summary>
        /// Returns the smallest AABB that contains the union of the given AABB's.
        /// </summary>
        /// <param name="bbs">A set of AABB's.</param>
        public static AABB Bound(params AABB[] bbs)
        {
            return Bound((IEnumerable<AABB>)bbs);
        }

        /// <summary>
        /// Computes the intersection of two intervals.
        /// </summary>
        /// <returns>A pair (o, s) that marks the origin and size of the intersection interval. Size is nonnegative. If the interval is empty, o is NaN and e is 0.</returns>
        /// <param name="o1">The origin of the first interval.</param>
        /// <param name="s1">The size of the first interval.</param>
        /// <param name="o2">The origin of the second interval.</param>
        /// <param name="s2">The size of the second interval.</param>
        private static (double, double) intersectDimension(double o1, double s1, double o2, double s2)
        {
            var o = Math.Max(o1, o2);
            var e = Math.Min(s1, s2) - o;

            return e < 0 ? (double.NaN, 0) : (o, e);
        }

        /// <summary>
        /// Computes the intersection of a set of AABB's.
        /// </summary>
        /// <param name="bbs">A set of AABB's.param>
        public static AABB Intersect(IEnumerable<AABB> bbs)
        {
            var i = Full;

            foreach (var b in bbs)
            {
                if (i.IsEmpty)
                    break;

                var o = b.Origin;
                var e = o + b.Size;

                var x = intersectDimension(i.Origin.X, i.Size.X, b.Origin.X, b.size.X);
                var y = intersectDimension(i.Origin.Y, i.Size.Y, b.Origin.Y, b.size.Y);
                var z = intersectDimension(i.Origin.Z, i.Size.Z, b.Origin.Z, b.size.Z);

                o = new Vector3(x.Item1, y.Item1, z.Item1);
                i = new AABB(o, Vector3.IsNaV(o) ? new Vector3() : new Vector3(x.Item2, y.Item2, z.Item2));
            }

            return i;
        }

        /// <summary>
        /// Computes the intersection of a set of AABB's.
        /// </summary>
        /// <param name="bbs">A set of AABB's.param>
        public static AABB Intersect(params AABB[] bbs)
        {
            return Intersect((IEnumerable<AABB>)bbs);
        }

        public bool Equals(AABB other)
        {
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
        /// <remarks>
        /// An empty AABB will return <see cref="Vector3.NaV"/>.
        /// An ABB that is unbounded in at least one dimension can return either a value or negative infinity
        /// for its origin in that dimension.
        /// </remarks>
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
        /// If the AABB is empty or contains only one single point, its size is 0.
        /// If the AABB is unbounded in at least one dimension, its size in that dimension
        /// is positive infinity.
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
                return Vector3.IsNaV(origin);
            }
        }

        /// <summary>
        /// Indicates whether this AABB contains all the points of 3D space.
        /// </summary>
        public bool IsFull
        {
            get
            {
                return origin == Full.Origin && size == Full.Size;
            }
        }

        public override string ToString()
        {
            if (this.IsEmpty)
                return "empty";
            if (this.IsFull)
                return "full";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}x{1}x{2}@{3}", size.X, size.Y, size.Z, origin);
        }
    }
}
