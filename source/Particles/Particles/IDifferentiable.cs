using System;
namespace Particles
{
    /// <summary>
    /// A physical quantity the gradient of which is known.
    /// </summary>
    /// <typeparam name="Q">The type that represents amounts of this quantity.</typeparam>
    /// <typeparam name="G">The type that represents values of the gradient for this quantity.</typeparam>
    public interface IDifferentiable<Q, G> : IQuantity<Q> where Q : IQuantity<Q> where G : IGradient<Q, G>
    {
        /// <summary>
        /// Returns the gradient of this quantity, i.e. a measure for how
        /// the quantity develops over time.
        /// </summary>
        /// <returns>The gradient of this quantity.</returns>
        G GetGradient();

        /// <summary>
        /// Returns the product (1 / <paramref name="dt"/>) * (<paramref name="q"/> + (-1) * <see cref="this"/>), i.e.
        /// the average rate at which this quantity must increase in order to reach <paramref name="q"/> within time
        /// <paramref name="dt"/>.
        /// </summary>
        /// <returns>The product (1 / <paramref name="dt"/>) * (<paramref name="q"/> + (-1) * <see cref="this"/>)</returns>
        /// <param name="dt">A time span.</param>
        /// <param name="q">A quantity.</param>
        G Differentiate(double dt, Q q);
    }
}
