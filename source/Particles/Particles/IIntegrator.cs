using System;

namespace Particles
{
    /// <summary>
    /// A method for computing the behavior of a physical quantity over time.
    /// </summary>
    /// <typeparam name="Q">The type that represents amounts of the quantity.</typeparam>
    /// <typeparam name="G">The type that represents values of the gradient for the quantity.</typeparam>
    public interface IIntegrator<Q, G> where Q : IDifferentiable<Q, G> where G : IGradient<Q, G>
    {
        /// <summary>
        /// Integrates the gradient of the given quantity over the given time span, updating the quantity.
        /// </summary>
        /// <param name="q">The quantity to update.</param>
        /// <param name="dt">The time span over which to integrate the gradient.</param>
        void Integrate(Q q, double dt);
    }
}