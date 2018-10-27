using System;
namespace Particles
{
    /// <summary>
    /// A physical quantity.
    /// </summary>
    /// <typeparam name="Q">The type that represents amounts of this quantity.</typeparam>
    public interface IQuantity<Q> where Q : IQuantity<Q>
    {
        /// <summary>
        /// Duplicates this quantity.
        /// </summary>
        /// <returns>A duplicate of this quantity</returns>
        Q Copy();

        /// <summary>
        /// Adds the given weighted quantity to this one, subject to the following constraints:
        /// 
        /// Let f * a be the state of a after a.Add(f - 1, a)
        /// and let a + b be the state of a after a.Add(1, b)
        ///  
        /// Then this operation must satisfy, for all real numbers f, g and all Q's a, b:
        /// 
        /// Additive Associativity: a + (b + c) = (a + b) + c
        /// Commutativity: a + b = b + a
        /// Neutral element: There is a state 0, s.t. a + 0 = a
        /// Inverse: a + (-1) * a = 0
        /// Multiplicative Associativity: (f * g) * a = f * (g * a) 
        /// Distributivity: (f + g) * a = f * a + g * a and f * (a + b) = f * a + f * b
        /// </summary>
        /// <remarks>
        /// The axioms entail:
        /// a + (f - 1) * a = a + f * a + (-1) * a = f * a
        /// 0 * a = a + (-1) * a = 0
        /// 1 * a = a + 0 * a = a
        /// 2 * a = (1 + 1) * a = 1 * a + 1 * a = a + a
        /// </remarks>
        /// <param name="f">A real number</param>
        /// <param name="b">A quantity</param>
        void Add(double f, Q b);
    }
}
