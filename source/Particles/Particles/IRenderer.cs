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
        /// Processes the given quantity. The result can be obtained via <see cref="RenderedState"/>.
        /// This method assumes that the given quantity evolved from the quantity
        /// it was last called for.
        /// </summary>
        /// <param name="q">A quantity to render.</param>
        void Render(Q q);

        /// <summary>
        /// The result of the last call to <see cref="Render(Q)"/>.
        /// </summary>
        T RenderedState
        {
            get;
        }
    }
}
