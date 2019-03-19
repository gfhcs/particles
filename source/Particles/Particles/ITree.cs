using System;
using System.Collections.Generic;

namespace Particles
{
    /// <summary>
    /// Represents a node in a tree.
    /// </summary>
    /// <remarks>A node in a tree is an object that knows a set of so-called children.
    /// The childeren of a node are nodes. If a node C is a child of a node P, P is called the parent of C.
    /// A node never has more than one parent.
    /// The child-relation is acyclic, i.e. by starting at some node and following the child relation it is
    /// impossible to reach that starting node again.</remarks>
    /// <typeparam name="T">The type that implements this interface.</typeparam>
    public interface ITreeNode<T> where T : ITreeNode<T>
    {
        /// <summary>
        /// Enumerates the child nodes of this node.
        /// </summary>
        IEnumerable<T> Children
        { get; }
    }

    /// <summary>
    /// Makes implementing objects accessable as a tree.
    /// </summary>
    /// <remarks>
    /// A tree is a set of nodes, see <see cref="ITreeNode{T}"/>.
    /// If this set is nonempty, there must be exactly one node in it without a parent, the so-called root.
    /// All the other nodes must have exactly one parent node.
    /// The child relation is acyclic, i.e. by starting at some node and following the child relation it is
    /// impossible to reach that starting node again.
    /// </remarks>
    /// <typeparam name="T">The type representing the nodes of the tree.</typeparam>
    public interface ITree<T> where T : ITreeNode<T>
    {
        /// <summary>
        /// The root node of the tree.
        /// </summary>
        /// <value>The root of the tree. null if the tree is empty.</value>
        T Root { get; } 
    }
}
