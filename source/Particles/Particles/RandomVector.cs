using System;
namespace Particles
{
    /// <summary>
    /// A random vector generator.
    /// </summary>
    public class RandomVector
    {
        private Random rnd;

        /// <summary>
        /// Creates a new random vector generator.
        /// </summary>
        /// <param name="rnd">Random.</param>
        public RandomVector(Random rnd)
        {
            this.rnd = rnd;
        }

        /// <summary>
        /// The underlying random number generator.
        /// </summary>
        /// <value>The random.</value>
        public Random Random
        {
            get { return rnd; }
        }

        /// <summary>
        /// Randomly samples a vector from a uniformly distributed ball.
        /// </summary>
        /// <returns>The sampled vector.</returns>
        /// <param name="range">The maximum magnitude the vector should have.</param>
        public Vector3 NextVector(double range)
        {
            var d = 2 * Math.PI * rnd.NextDouble();
            var a1 = Math.Cos(d) * new Vector3(1, 0, 0) + Math.Sin(d) * new Vector3(0, 1, 0);

            var r = 2 * rnd.NextDouble() - 1;

            var e = Math.Sqrt(Math.Cos(Math.PI * Math.Abs(r) / 2));
            var a2 = e * a1 + Math.Sign(r) * Math.Sin(Math.Acos(e)) * new Vector3(0, 0, 1);

            return range * Math.Pow(rnd.NextDouble(), 1.0 / 3.0) * a2;
        }

    }
}
