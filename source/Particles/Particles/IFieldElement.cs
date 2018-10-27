using System;
namespace Particles
{

    /// <summary>
    /// An element of a mathematical field.
    /// </summary>
    public interface IFieldElement<T>
    {
        /// <summary>
        /// Creates and returns a copy of this field element.
        /// </summary>
        T Copy();

        /// <summary>
        /// Makes this element the neutral element of addition, i.e.
        /// for every other element e the call sequence
        /// this.SetToZero(); this.Add(e)
        /// makes this element equal to e.
        /// </summary>
        void SetToZero();

        /// <summary>
        /// Makes this element the neutral element of multiplication, i.e.
        /// for every other element e the call sequence
        /// this.SetToOne(); this.Multiply(e)
        /// makes this element equal to e.
        /// </summary>
        void SetToOne();

        /// <summary>
        /// An operation that is associative and commutative, i.e.
        /// a.Add(b) leaves a in the same state as b.Add(a) leaves b.
        /// and
        /// a.Add(b); a.Add(c) leaves a in the same state as b.Add(c); a.Add(b)
        /// </summary>
        /// <param name="e">A field element to be added to this one.</param>
        void Add(T e);

        /// <summary>
        /// Turns this field element into its additive inverse, i.e.
        /// this.Copy().Add(this) = 0
        /// </summary>
        void InvertA();

        /// <summary>
        /// An operation that is associative and commutative, i.e.
        /// a.Multiply(b) leaves a in the same state as b.Multiply(a) leaves b.
        /// and
        /// a.Multiply(b); a.Multiply(c) leaves a in the same state as b.Multiply(c); a.Multiply(b)
        /// Also this operation is distributive with <see cref="Add"/>:
        /// b.Add(c); a.Multiply(b) leaves a in the same state as c.Multiply(a); a.Multiply(b); a.Add(c);
        /// b.Add(c); b.Multiply(a) leaves b in the same state as a.Multiply(c); b.Multiply(a); b.Add(a);
        /// </summary>
        /// <param name="e">A field element to be multiplied to this one.</param>
        void Multiply(T e);

        /// <summary>
        /// Turns this field element into its multiplicative inverse, i.e.
        /// this.Copy().Multiply(this) = 1
        /// </summary>
        void InvertM();
    }
}