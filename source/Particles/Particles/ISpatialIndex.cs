using System.Collections.Generic;

namespace Particles
{
    /// <summary>
    /// Represents a node in a spatial index.
    /// </summary>
    public interface IIndexNode<N, T> : ITreeNode<N> where N : IIndexNode<N, T>
    {
        /// <summary>
        /// Enumerates all the items along with their positions that lie within
        /// the spatial range represented by this node.
        /// </summary>
        IEnumerable<(T, Vector3)> Items
        { get; }

        /// <summary>
        /// Indicates if this node is a leaf, i.e. has no children.
        /// </summary>
        /// <value><c>true</c> if this node has no children; otherwise, <c>false</c>.</value>
        bool IsLeaf { get;}

        /// <summary>
        /// Returns the number of children of this node.
        /// </summary>
        int Arity { get; }
    }

    /// <summary>
    /// A structure that hierarchically groups objects scattered across space, such that smaller groups form larger groups.
    /// The closer two objects are in space, the more likely they are to be in the same groups.
    /// </summary>
    /// <remarks>
    /// A spatial index is supposed to simplify iteration over the objects contained in a space: Iteration can choose
    /// whether certain groups of objects are worth recursively descending into, or not. Thus it is easy to skip objects
    /// that are not of interest.
    /// </remarks>
    public interface ISpatialIndex<N, T> : ITree<N> where N : IIndexNode<N, T>
    {
        /// <summary>
        /// The number of items stored in this index.
        /// </summary>
        int ItemCount { get;  }
    }
}