using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Threading;

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
    /// <typeparam name="N">The type that implements this interface.</typeparam>
    public interface ITreeNode<N> where N : ITreeNode<N>
    {
        /// <summary>
        /// Enumerates the child nodes of this node.
        /// </summary>
        IEnumerable<N> Children
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
    /// <typeparam name="N">The type representing the nodes of the tree.</typeparam>
    public interface ITree<N> where N : ITreeNode<N>
    {
        /// <summary>
        /// The root node of the tree.
        /// </summary>
        /// <value>The root of the tree. null if the tree is empty.</value>
        N Root { get; } 
    }


    public static class ITreeNodeExtensions
    {
        #region "Simple properties"

        /// <summary>
        /// Decides if this node a leaf node, i.e. if it has no children.
        /// </summary>
        /// <returns><c>true</c>, if the node has no children and thus is a leaf node, <c>false</c> otherwise.</returns>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static bool IsLeaf<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            return !node.Children.Any();
        }

        /// <summary>
        /// Returns the number of children of this node.
        /// </summary>
        /// <returns>A nonnegative number.</returns>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static int Arity<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            return node.Children.Count();
        }

        #endregion

        #region "Fold"

        /// <summary>
        /// Folds the subtree under this node.
        /// </summary>
        /// <remarks>
        /// Folding a (sub-)tree means computing a value for its root node by recursively folding the subtrees under its children. This means that for each node in the tree,
        /// the subtrees under the children are folded first, yielding a sequence of values (one per child) that is then processed in order to obtain a value for the common parent node.
        /// This processing is defined by <paramref name="f"/>.
        /// </remarks>
        /// <param name="node">A tree node.</param>
        /// <param name="f">A procedure that processes values computed for the children of a node in order to obtain a value for that node.
        /// It is called once after the child subtrees of a node have all been folded. Its parameters are the current node and the sequence of values obtained for the children.</param>
        /// <typeparam name="F">The return type of <paramref name="f"/>.</typeparam>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static F Fold<N, F>(this ITreeNode<N> node, Func <N, IEnumerable<F>, F> f) where N : ITreeNode<N>
        {
            return f((N)node, from c in node.Children select c.Fold(f));
        }

        /// <summary>
        /// Folds the subtree under this node.
        /// </summary>
        /// <remarks>
        /// Folding a (sub-)tree means computing a value for its root node by recursively folding the subtrees under its children. This means that for each node in the tree,
        /// the subtrees under the children are folded first, yielding a sequence of values (one per child) that is then processed in order to obtain a value for the common parent node.
        /// This processing is defined by <paramref name="f"/>.
        /// </remarks>
        /// <param name="node">A tree node.</param>
        /// <param name="f">
        /// A procedure that processes values computed for the children of a node in order to obtain a value for that node. Its first parameter is the current node, its second parameter
        /// is an accumulator, and its third parameter is set to the value computed for one child of the current node node.
        /// For each node, <paramref name="f"/> is called once per child, with the accumulator always holding the value returned for the previous child, or the value <paramref name="initial"/>, in the case of the very first child.
        /// Note that <paramref name="f"/> will not be called if the current node has no children, and thus the value <paramref name="initial"/> will be the end result for that node!</param>
        /// <param name="initial">
        /// The initial value for the accumulator argument of <paramref name="f"/>, i.e. the value the accumulator
        /// should have when <paramref name="f"/> is called for the first child of a node.
        /// </param>
        /// <typeparam name="F">The return type of <paramref name="f"/>.</typeparam>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static F Fold<N, F>(this ITreeNode<N> node, Func<N, F, F, F> f, F initial) where N : ITreeNode<N>
        {
            var acc = initial;

            foreach (var c in node.Children)
                acc = f((N)node, acc, c.Fold(f, initial));

            return acc;
        }

        /// <summary>
        /// Folds the subtree under this node in a concurrent way.
        /// </summary>
        /// <remarks>
        /// This method has the same semantics as <see cref="Fold{N, F}(N, Func{N, IEnumerable{F}, F})"/>.
        /// However, it processes the tree in a parallel fashion, i.e. the children of a node are not always processes one after another, but
        /// may be processed in parallel, depending on the size of the tree and the level of parallelism currently available on the machine.
        /// </remarks>
        /// <param name="node">A tree node.</param>
        /// <param name="f">A procedure that processes values computed for the children of a node in order to obtain a value for that node.
        /// It is called once after the child subtrees of a node have all been folded. Its parameters are the current node and the sequence of values obtained for the children.</param>
        /// <typeparam name="F">The return type of <paramref name="f"/>.</typeparam>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static Task<F> Fold<N, F>(this ITreeNode<N> node, Func<N, IEnumerable<Task<F>>, Task<F>> f) where N : ITreeNode<N>
        {
            /*
             * This is my best shot so far at a manual implementation. However, I figured that the parallel distribution of work
             * I am attempting here is exactly what the Task API must be able to do. And probably that API is better at it.
             * 
            int busy = 0;

            bool reserve()
            {
                if (Interlocked.Increment(ref busy) > Environment.ProcessorCount)
                {
                    Interlocked.Decrement(ref busy);
                    return false;
                }
                return true;
            }

            F work(N n, bool initial)
            {
                var childJobs = new Task<F>[n.Arity()];

                var i = 0;
                foreach (var c in n.Children)
                    childJobs[i++] = reserve() ? Task.Run(() => work(c, true)) : Task.FromResult(work(c, false));

                Interlocked.Decrement(ref busy); // Other people might still be working on jobs for me, but I am only waiting at this point!
                var fs = await Task.WhenAll(childJobs);
                Interlocked.Increment(ref busy); // I am done waiting.

                var result = f(n, fs);

                if (initial)
                    Interlocked.Decrement(ref busy);

                return result;
            }

            return work(node);

            */

            var childTasks = new Task<F>[node.Arity()];

            var i = 0;
            foreach (var c in node.Children)
                childTasks[i++] = c.Fold(f);

            return f((N)node, childTasks);
        }

        #endregion

        #region "Usages of Fold"

        /// <summary>
        /// Computes the height of this node, i.e. the number of nodes contained in the longest path starting at this node
        /// and leading to one of the leaves in its subtree.
        /// </summary>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static int Height<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            return node.Fold((N _, IEnumerable<int> chs) => 1 + chs.DefaultIfEmpty().Max());
        }

        /// <summary>
        /// Computes the height of this tree, i.e. the number of nodes contained in the longest path starting at the root
        /// and leading to one of the leaves. 0 is returned if and only if the tree is empty.
        /// </summary>
        /// <param name="tree">A tree.</param>
        /// <typeparam name="N">The type of the nodes in <paramref name="tree"/>.</typeparam>
        public static int Height<N>(this ITree<N> tree) where N : ITreeNode<N>
        {
            return tree.Root?.Height() ?? 0;
        }

        /// <summary>
        /// Computes the size of the subtree under this node, i.e. the number of nodes contained in this subtree.
        /// </summary>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static int Size<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            return node.Fold((N _, IEnumerable<int> chs) => 1 + chs.Sum());
        }

        /// <summary>
        /// Computes the size of this tree, i.e. the number of nodes it contains.
        /// </summary>
        /// <param name="tree">A tree.</param>
        /// <typeparam name="N">The type of the nodes in <paramref name="tree"/>.</typeparam>
        public static int Size<N>(this ITree<N> tree) where N : ITreeNode<N>
        {
            return tree.Root?.Size() ?? 0;
        }

        /// <summary>
        /// Computes the width of the subtree under this node, i.e. the number of leaf nodes contained in this subtree.
        /// </summary>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static int Width<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            return node.Fold((N _, IEnumerable<int> chs) => Math.Max(1, chs.Sum()));
        }

        /// <summary>
        /// Computes the width of this tree, i.e. the number of leaf nodes it contains.
        /// </summary>
        /// <param name="tree">A tree.</param>
        /// <typeparam name="N">The type of the nodes in <paramref name="tree"/>.</typeparam>
        public static int Width<N>(this ITree<N> tree) where N : ITreeNode<N>
        {
            return tree.Root?.Width() ?? 0;
        }


        #endregion

        /// <summary>
        /// Enumerates all the leaves in the subtree under this node.
        /// </summary>
        /// <param name="node">A tree node.</param>
        /// <typeparam name="N">The type of <paramref name="node"/>.</typeparam>
        public static IEnumerable<N> Leaves<N>(this ITreeNode<N> node) where N : ITreeNode<N>
        {
            // We use an explicit stack to avoid a recursive iterator.
            // Recursive iterators create hassle for the GC and are slow.

            var childrenArray = new N[16];

            var stack = new Stack<N>();
            stack.Push((N)node);

            while (stack.Any())
            {
                var n = stack.Pop();
                var cc = 0;

                // We need to reverse the children, which is why we store them in an array.
                foreach (var c in n.Children)
                {
                    if (cc == childrenArray.Length)
                        Array.Resize(ref childrenArray, 2 * childrenArray.Length);
                    childrenArray[cc++] = c;
                }

                if (cc == 0)
                    yield return n;
                else
                    while (cc > 0)
                        stack.Push(childrenArray[--cc]);
            }
        }

        /// <summary>
        /// Enumerates all the leaves of this tree.
        /// </summary>
        /// <param name="tree">A tree.</param>
        /// <typeparam name="N">The type of the nodes in <paramref name="tree"/>.</typeparam>
        public static IEnumerable<N> Leaves<N>(this ITree<N> tree) where N : ITreeNode<N>
        {
            return tree.Root?.Leaves() ?? new N[0];
        }

    }
}
