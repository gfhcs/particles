using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// An explicit Runge-Kutta integrator.
    /// </summary>
    /// <remarks>
    /// Explicit Runge Kutta integrators update the quantity according to
    /// y' = y + dt * (b_1 * k_1 + ... + b_s * k_s)
    /// where the b_j are weights and
    /// k_j = f(t + dt * c_j, y + dt * (a_j1 * k1 + ... + a_jj * kj))
    /// with c_j being coefficients as well.
    /// </remarks>
    /// <typeparam name="Q">The type that represents amounts of the quantity.</typeparam>
    /// <typeparam name="G">The type that represents values of the gradient for the quantity.</typeparam>
    public class RungeKuttaIntegrator<Q, G> : IIntegrator<Q, G> where Q : IDifferentiable<Q, G> where G : IGradient<Q, G>
    {
        private readonly ImmutableArray<long> butcher;
        private readonly long d;
        /// <summary>
        /// The number of stages of this Runge-Kutta method.
        /// </summary>
        private readonly int s;
        private readonly G[] k;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Particles.RungeKuttaIntegrator`2"/> class.
        /// </summary>
        /// <exception cref="ArgumentException">If <paramref name="butcher"/> does not have the right dimensions, or if <paramref name="d"/> is an illegal divisor.</exception>
        /// <param name="butcher">
        /// The butcher tableau for this Runge-Kutta integrator.
        /// It contains coefficients a, b, and c, in this order.
        /// a is listed row, by row. Entries above the diagnoal are omitted, because they need to be zero anyway for the method to be explicit.
        /// b and c must both have as many entries as a has rows and columns.
        /// </param>
        /// <param name="d">The common divisor for all the coefficients in <paramref name="butcher"/></param>
        protected RungeKuttaIntegrator(ImmutableArray<long> butcher, long d)
        {
            if (d == 0)
                throw new DivideByZeroException("The divisor must not be 0!");
            if (d < 0)
                throw new ArgumentException("The divisor must not be negative!");

            var l = butcher.Length;

            this.butcher = butcher;
            this.d = d;
            this.s = (int)Math.Round(Math.Sqrt(8 * l - 25)) / 2;

            if ((s * s + 5 * s) / 2 != l)
                throw new ArgumentException("The given array does not have dimensions valid for a butcher tableau!");

            this.k = new G[s];
        }

        #region "Indexing"

        /// <summary>
        /// The number of stages of this method.
        /// </summary>
        public int StageCount
        {
            get
            {
                return s;
            }
        }

        /// <summary>
        /// The divisor by which all the coefficients of this method are to be divided.
        /// </summary>
        public long Divisor
        {
            get
            {
                return d;
            }
        }

        private long a(int i, int j)
        {
            return butcher[(i + 1) * i / 2 + j];
        }

        /// <summary>
        /// Returns coefficient a_{<paramref name="i"/>, <paramref name="j"/>} of this Runge-Kutta method.
        /// </summary>
        /// <param name="i">The row index inside A. Must be in [0 : s - 1]</param>
        /// <param name="j">The column index inside A. Must be in [0 : s - 1]</param>
        /// <exception cref="IndexOutOfRangeException">If <paramref name="i"/> or <paramref name="j"/> are not within [0 : s - 1].</exception>
        public long GetA(int i, int j)
        {
            if (!(i >= 0 && j >= 0))
                throw new IndexOutOfRangeException("Indices must not be negative!");
            if (!(i < s && j < s))
                throw new IndexOutOfRangeException(string.Format("Indices for the A matrix of a {0}-stage Runge-Kutta method must be less than {0}!", s));

            return j > i ? 0 : a(i, j);
        }

        private long b(int j)
        {
            return butcher[butcher.Length - 2 * s + j];
        }

        /// <summary>
        /// Returns coefficient b_<paramref name="j"/> of this Runge-Kutta method.
        /// </summary>
        /// <param name="j">The index inside b. Must be in [0 : s - 1]</param>
        /// <exception cref="IndexOutOfRangeException">If <paramref name="j"/> is not within [0 : s].</exception>
        public long GetB(int j)
        {
            if (!(j >= 0))
                throw new IndexOutOfRangeException("Index must not be negative!");
            if (!(j < s))
                throw new IndexOutOfRangeException(string.Format("Index {0} for an {1}-stage Runge-Kutta method must be less than {1}!", nameof(j), s));

            return b(j);
        }

        private long c(int j)
        {
            return butcher[butcher.Length - s + j];
        }

        /// <summary>
        /// Returns coefficient c_<paramref name="j"/> of this Runge-Kutta method.
        /// </summary>
        /// <param name="j">The index inside b. Must be in [0 : s - 1]</param>
        /// <exception cref="IndexOutOfRangeException">If <paramref name="j"/> is not within [0 : s].</exception>
        public long GetC(int j)
        {
            if (!(j >= 0))
                throw new IndexOutOfRangeException("Index must not be negative!");
            if (!(j < s))
                throw new IndexOutOfRangeException(string.Format("Index {0} for an {1}-stage Runge-Kutta method must be less than {1}!", nameof(j), s));

            return c(j);
        }
        #endregion

        public void Integrate(Q q, double dt)
        {
            for (int j = 0; j < s; j++)
            {
                var p = q.Copy();

                for (int l = 0; l < j; l++)
                    k[l].Add(p, dt * a(j, l) / d);

                k[j] = p.GetGradient();
            }

            for (int j = 0; j < s; j++)
                k[j].Add(q, dt * b(j) / d);
        }
    }
}
