using System;
using System.Collections.Immutable;

namespace Particles
{
    /// <summary>
    /// The classical Runge-Kutta method, of order 4.
    /// </summary>
    public class RK4<Q, G> : RungeKuttaIntegrator<Q, G> where Q : IDifferentiable<Q, G> where G : IGradient<Q, G>
    {
        private static readonly ImmutableArray<long> butcher = new long[] {0, 3, 0, 0, 3, 0, 0, 0, 6, 0, 1, 2, 2, 1, 0, 3, 3, 6}.ToImmutableArray();

        public RK4() : base(butcher, 6)
        {
        }
    }
}
