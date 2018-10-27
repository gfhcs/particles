using System;
namespace Particles
{
    /// <summary>
    /// A gradient quantity, i.e. the rate at which the underlying quantity changes.
    /// </summary>
    /// <typeparam name="Q">The type that represents amounts of the quantity.</typeparam>
    /// <typeparam name="G">The type that represents values of the gradient for the quantity.</typeparam>
    public interface IGradient<Q, G> : IQuantity<G> where Q : IQuantity<Q> where G : IGradient<Q, G>
    {
        /// <summary>
        /// Propagates <paramref name="q"/> by adding <paramref name="dt"/> * <see cref="this"/> to it.
        /// This operation must obey the same laws as <see cref="IQuantity{Q}.Add(double, Q)"/>.
        /// </summary>
        /// <param name="q">A quantity.</param>
        /// <param name="dt">A time span.</param>
        void Add(Q q, double dt);
    }
}
