using System;
namespace Particles
{
    /// <summary>
    /// A vector of three real numbers.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>, IComparable<Vector3>
    {
        private readonly double x, y, z;

        /// <summary>
        /// A value that is "not a vector".
        /// </summary>
        public static readonly Vector3 NaV = new Vector3(double.NaN, double.NaN, double.NaN);

        /// <summary>
        /// Indicates whether the given value is "not a vector".
        /// </summary>
        /// <returns><c>true</c>, if the value is not a proper vector, <c>false</c> if it is.</returns>
        /// <param name="v">A vector value.</param>
        public static bool IsNaV(Vector3 v)
        {
            return double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsNaN(v.Z);
        }

        /// <summary>
        /// Creates a new Vector 3.
        /// </summary>
        /// <param name="x">The x component.</param>
        /// <param name="y">The y component.</param>
        /// <param name="z">The z component.</param>
        public Vector3(double x = 0.0, double y = 0.0, double z = 0.0)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// The x component.
        /// </summary>
        public double X
        {
            get { return x; }
        }

        /// <summary>
        /// The y component.
        /// </summary>
        public double Y
        {
            get { return y; }
        }

        /// <summary>
        /// The z component.
        /// </summary>
        public double Z
        {
            get { return z; }
        }

        /// <summary>
        /// Returns the length of the vector in the Euclidean space.
        /// </summary>
        public double Magnitude
        {
            get {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        #region "Equality"

        public int CompareTo(Vector3 other)
        {
            var c = IsNaV(this).CompareTo(IsNaV(other));
            if (c != 0) return c;
            c = this.x.CompareTo(other.x);
            if (c != 0) return c;
            c = y.CompareTo(other.y);
            if (c != 0) return c;
            return z.CompareTo(other.z);
        }

        public bool Equals(Vector3 other)
        {
            return IsNaV(this).Equals(IsNaV(other)) && this.x.Equals(other.x) && this.y.Equals(other.y) && this.z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector3))
                return false;
                    
            var other = (Vector3)obj;
            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return IsNaV(this) ? 0 : (31 * x.GetHashCode() + 7 * y.GetHashCode() + z.GetHashCode());
        }

        public static bool operator ==(Vector3 a, Vector3 b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Vector3 a, Vector3 b)
        {
            return !a.Equals(b);
        }

        #endregion

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0}, {1}, {2})", x, y, z);
        }

        #region "Arithmetic"

        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vector3 operator *(double f, Vector3 a)
        {
            return new Vector3(f * a.x, f * a.y, f * a.z);
        }

        public static Vector3 operator *(Vector3 a, double f)
        {
            return new Vector3(f * a.x, f * a.y, f * a.z);
        }

        public static double operator *(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static Vector3 operator /(Vector3 a, double f)
        {
            return new Vector3(a.x / f, a.y / f, a.z / f);
        }

        #endregion
    }
}
