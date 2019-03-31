namespace Particles
{
    /// <summary>
    /// A method of processing subsequent states of a quantity.
    /// </summary>
    /// <typeparam name="Q">The type of the quantity to be processed. </typeparam>
    /// <typeparam name="T">The type of the result of the rendering. </typeparam>
    public interface IRenderer<Q, T> where Q : IQuantity<Q>
    {
        /// <summary>
        /// Processes the given quantity, yielding a rendering result.
        /// This method assumes that the given quantity evolved from the quantity
        /// it was last called for.
        /// </summary>
        /// <returns>The rendering result.</returns>
        /// <param name="q">A quantity to render.</param>
        T Render(Q q);
    }
}
