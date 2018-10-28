using System;
namespace Particles
{
    /// <summary>
    /// A vector of three real numbers.
    /// </summary>
    public struct Vector3
    {
        private readonly double x, y, z;

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
