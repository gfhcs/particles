using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace Particles
{   
    /// <summary>
    /// Represents a node in an octree.
    /// </summary>
    public interface IOctreeNode<T>
    {
        /// <summary>
        /// Enumerates the child nodes of this node.
        /// </summary>
        IEnumerable<IOctreeNode<T>> Children
        { get; }

        /// <summary>
        /// Enumerates all the items along with their positions that lie within
        /// the spatial range represented by this node.
        /// </summary>
        IEnumerable<(T, Vector3)> Items
        { get; }

        /// <summary>
        /// Indicates whether this node is a leaf node.
        /// </summary>
        bool IsLeaf
        { get; }
    }

    /// <summary>
    /// A tree each node of which represents a subset of three-dimensional-space.
    /// Each node can have up to 8 children (hence the name).
    /// The space represented by a node is an axis-aligned bounding box (AABB).
    /// The tree is created for a set of objects that each have position in space.
    /// Each node volume contains at least one of these objects.
    /// </summary>
    /// <remarks>
    /// This class does not allow decorating the nodes of the tree. For this use
    /// the class <see cref="LabelledOctree"/> (not existing yet).
    /// </remarks>
    /// <typeparam name="T">The type of the objects scattered across space.</typeparam>
    public class Octree<T>
    {
        #region "Morton code"
        private const uint max = 2097152; // 2^21

        /// <summary>
        /// Turns a 21-bit integer into a 63-bit integer by inserting 2 zeros before each bit.
        /// </summary>
        /// <returns>A 21-bit integer.</returns>
        /// <param name="x">An integer in the range [0 : 2^21 - 1]</param>
        private static ulong spread3(ulong x)
        {
            x = ((x << 32) | x) & 0xFFFF00000000FFFFu;
            x = ((x << 16) | x) & 0x00FF0000FF0000FFu;
            x = ((x << 8) | x) & 0xF00F00F00F00F00Fu;
            x = ((x << 4) | x) & 0x30C30C30C30C30C3u;
            x = ((x << 2) | x) & 0x4924924949249249u;

            return x;
        }

        /// <summary>
        /// Computes the one-dimensional 63-bit Morton code for the given three-dimensional vector.
        /// </summary>
        /// <remarks>
        /// Morton codes order vectors by depth-first traversal of an octree.
        /// </remarks>
        /// <returns>The morton code.</returns>
        /// <param name="v">A vector from the unit cube (all components within [0 ; 1])</param>
        private static ulong morton(Vector3 v)
        {
            var x = spread3((uint)Math.Min(Math.Max(v.X * max, 0), max - 1));
            var y = spread3((uint)Math.Min(Math.Max(v.Y * max, 0), max - 1));
            var z = spread3((uint)Math.Min(Math.Max(v.Z * max, 0), max - 1));
            return (x << 2) | (y << 1) | z;
        }

        /// <summary>
        /// Counts the number of set bits in the given integer.
        /// </summary>
        private static int popcount(ulong i)
        {
            i = i - ((i >> 1) & 0x5555555555555555UL);
            i = (i & 0x3333333333333333UL) + ((i >> 2) & 0x3333333333333333UL);
            return (int)(unchecked(((i + (i >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        /// <summary>
        /// Sets all bits after the first 1 bit to 1.
        /// </summary>
        private static ulong smear(ulong x)
        {
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x;
        }

        /// <summary>
        /// Counts the leading zeros of the binary representation of a number.
        /// </summary>
        /// <returns>The number of leading zeros in the 64 binary representation of <paramref name="x"/>.</returns>
        /// <param name="x">A number</param>
        private static int countLeadingZeros(ulong x)
        {
            return 64 - popcount(smear(x));
        }

        /// <summary>
        /// Computes a measure for how different two Morton codes are.
        /// </summary>
        /// <returns>A number. The higher the number, the more different the Morton codes at indices <paramref name="i"/> and <paramref name="j"/> are. A value of 0 indicates perfect equality.</returns>
        /// <param name="mortonCodes">An array of Morton codes.</param>
        /// <param name="i">An index into <paramref name="mortonCodes"/>.</param>
        /// <param name="j">An index into <paramref name="mortonCodes"/>.</param>
        private static int delta(ulong[] mortonCodes, int i, int j)
        {
            if (!(0 <= i && i < mortonCodes.Length && 0 <= j && j < mortonCodes.Length))
                return 64;
            var mi = mortonCodes[i];
            var mj = mortonCodes[j];

            return 64 - countLeadingZeros(mi ^ mj);
        }

        /*
        /// <summary>
        /// Compares the child boxes of that Morton code box that contains both given Morton codes.
        /// </summary>
        /// <returns>A number in the range [-7:7]. 0 means that the given Morton codes are equal.</returns>
        /// <param name="mortonCodes">Morton codes.</param>
        /// <param name="i">The index.</param>
        /// <param name="j">J.</param>
        private static int childCompare(ImmutableArray<uint> mortonCodes, int i, int j)
        {
            if (!(0 <= i && i < mortonCodes.Length && 0 <= j && j < mortonCodes.Length))
                return 64;
            var mi = mortonCodes[i];
            var mj = mortonCodes[j];

            var d = mi ^ mj;

            var s = (64 - (countLeadingZeros(mi ^ mj) / 3) * 3) - 3;

            return (int) (((mj >> s) & 0x000000000000007UL) - ((mi >> s) & 0x000000000000007UL));

        }
        */

        #endregion

        /// <summary>
        /// A leaf node in an octree.
        /// </summary>
        /// <remarks>
        /// This struct cannot implement <see cref="IOctreeNode{T}"/>, because it does not know
        /// its own index in the octree. That's because storing this index in this struct would
        /// inflate the memory occupied by the octree unnecessarily.
        /// For a type implementing  <see cref="IOctreeNode{T}"/> refer to <see cref="OctreeNodeReference"/> instead!
        /// </remarks>
        protected struct LeafNode
        {
            private readonly T item;
            private readonly Vector3 position;
            private int nextSiblingDelta;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Particles.Octree`1.LeafNode"/> struct.
            /// </summary>
            /// <param name="item">The item represented by this leaf node.</param>
            /// <param name="position">The position at which the item represented by this leaf node is located.</param>
            public LeafNode(T item, Vector3 position)
            {
                this.item = item;
                this.position = position;
                this.nextSiblingDelta = 0;
            }

            /// <summary>
            /// The item this leaf node represents.
            /// </summary>
            public T Item
            {
                get { return item; }
            }

            /// <summary>
            /// The position at which the item represented by this leaf node is located.
            /// </summary>
            public Vector3 Position
            {
                get { return Position; }
            }

            /// <summary>
            /// A delta to be added to the index of this node in order to obtain the
            /// index of the first sibling to the right of this node.
            /// 0 indicates that there is no sibling to the right.
            /// Indices beyond the length of <see cref="leafNodes"/> indicate
            /// that the sibling is to be found in <see cref="internalNodes"/>.
            /// </summary>
            public int RightSiblingDelta
            {
                get
                {
                    return nextSiblingDelta;
                }
                set
                {
                    nextSiblingDelta = value;
                }
            }
        }

        /// <summary>
        /// A node of an octree.
        /// </summary>
        /// <remarks>
        /// This struct cannot implement <see cref="IOctreeNode{T}"/>, because it does not know
        /// its own index in the octree. That's because storing this index in this struct would
        /// inflate the memory occupied by the octree unnecessarily.
        /// For a type implementing  <see cref="IOctreeNode{T}"/> refer to <see cref="OctreeNodeReference"/> instead!
        /// </remarks>
        protected struct InternalNode
        {
            private int firstChildDelta;
            private int nextSiblingDelta;

            /// <summary>
            /// A delta to be added to the index of this node in order to obtain the
            /// index of the first child of this node.
            /// Indices below zero indicate
            /// that the child is to be found in <see cref="leafNodes"/>.
            /// </summary>
            public int FirstChildDelta
            {
                get
                {
                    return firstChildDelta;
                }
                set
                {
                    firstChildDelta = value;
                }
            }

            /// <summary>
            /// A delta to be added to the index of this node in order to obtain the
            /// index of the first sibling to the right of this node.
            /// 0 indicates that there is no sibling to the right.
            /// Indices below zero indicate
            /// that the sibling is to be found in <see cref="leafNodes"/>.
            /// </summary>
            public int RightSiblingDelta
            {
                get
                {
                    return nextSiblingDelta;
                }
                set
                {
                    nextSiblingDelta = value;
                }
            }
        }

        /// <summary>
        /// A reference to one of the nodes of an Octree.
        /// </summary>
        protected struct OctreeNodeReference : IOctreeNode<T>
        {
            private readonly Octree<T> owner;
            private readonly int index;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Particles.Octree`1.OctreeNodeReference"/> struct.
            /// </summary>
            /// <param name="owner">The octree into which this reference points.</param>
            /// <param name="index">
            /// The index of the node this reference points to. Positive indices point into <see cref="internalNodes"/>,
            /// while negative indices point into <see cref="leafNodes"/>, where -1 refers to the last element.
            /// </param>
            public OctreeNodeReference(Octree<T> owner, int index)
            {
                this.owner = owner;
                this.index = index;
            }

            /// <summary>
            /// The octree into which this reference points.
            /// </summary>
            /// <value>The owner.</value>
            public Octree<T> Owner
            {
                get
                {
                    return owner;
                }
            }

            /// <summary>
            /// The index of the node this reference points to. Positive indices point into <see cref="internalNodes"/>,
            /// while negative indices point into <see cref="leafNodes"/>, where -1 refers to the last element.
            /// </summary>
            public int Index
            { 
                get { return index; }
            }

            public IEnumerable<OctreeNodeReference> Children
            {
                get
                {
                    if (IsLeaf)
                        yield break;

                    var idx = index;
                    var delta = owner.internalNodes[index].FirstChildDelta;

                    while (delta != 0)
                    {
                        idx += delta;
                        yield return new OctreeNodeReference(owner, idx);
                        delta = idx < 0 ? owner.leafNodes[owner.leafNodes.Length + idx].RightSiblingDelta : owner.internalNodes[idx].RightSiblingDelta;
                    }
                }
            }

            IEnumerable<IOctreeNode<T>> IOctreeNode<T>.Children
            {
                get
                {
                    return this.Children.Cast<IOctreeNode<T>>();
                }
            }

            public IEnumerable<(T, Vector3)> Items
            {
                get
                {
                    var l = this.LastItemIndex;
                    for (int i = this.FirstItemIndex; i <= l; i++)
                    {
                        var leaf = owner.leafNodes[owner.LeafNodes.Length + index];
                        yield return (leaf.Item, leaf.Position);
                    }
                }
            }

            /// <summary>
            /// Returns the index of the first item below the subtree identified by this node reference.
            /// </summary>
            public int FirstItemIndex
            {
                get
                {
                    return IsLeaf ? index : Children.First().FirstItemIndex;
                }
            }

            /// <summary>
            /// Returns the index of the last item below the subtree identified by this node reference.
            /// </summary>
            public int LastItemIndex
            {
                get
                {
                    return IsLeaf ? index : Children.Last().LastItemIndex;
                }
            }

            public bool IsLeaf
            {
                get
                {
                    return index < 0;
                }
            }


        }

        /// <summary>
        /// Divides <paramref name="a"/> by <paramref name="b"/> and rounds up the result.
        /// </summary>
        /// <returns>The rounded up result of dividing <paramref name="a"/> by <paramref name="b"/></returns>
        private static int divUp(int a, int b)
        {
            return (a + b - 1) / b;
        }

        /// <summary>
        /// Initializes an internal node of an octree.
        /// </summary>
        /// <remarks>
        /// This method uses the properties of the Z-curve (Morton curve) in order to determine the position of the internal node in the tree.
        /// This method is safe to be called for all leaf indices (<paramref name="i1"/>) in parallel. After this method has been called for
        /// all leaf indices, the arras <paramref name="leaves"/> and <paramref name="internalNodes"/> are completely initialized.
        /// </remarks>
        /// <returns>A task object that can be awaited.</returns>
        /// <param name="mortonCodes">The sorted array of Morton codes for the objects indexed by the octree. May contain duplicates.</param>
        /// <param name="leaves">The array in which leave nodes for the octree are to be stored.</param>
        /// <param name="i1">The index of a leaf node. An internal node with the same index (in <paramref name="internalNodes"/> will be created.</param>
        /// <param name="internalNodes">The array in which internal nodes for the octree are to be stored.</param>
        private static void createInternalNode(ulong[] mortonCodes, int i1, LeafNode[] leaves, InternalNode[] internalNodes)
        {
            // Idea:
            // The i-th internal node sits at the edge of a box.
            // We find out the bounds of this box and then make this
            // internal node the root for this box.

            if (0 < i1 && mortonCodes[i1 - 1] == mortonCodes[i1])
                return;

            var i2 = i1;
            for (; i2 < mortonCodes.Length && mortonCodes[i2 + 1] == mortonCodes[i1]; i2++) { }

            // All nodes from i1 and i2 have the same Morton code.

            var d = Math.Sign(delta(mortonCodes, i1 - 1, i1) / 3 - delta(mortonCodes, i2, i2 + 1) / 3);

            // If d < 0, our internal node sits at the right edge of its leaf range.
            // If d > 0, our internal node sits at the left edge of its leaf range.
            // If d == 0, our internal node is neither the first, nor the last child of its parent.

            if (d == 0)
            {
                // This node only spans only one single morton code:
                internalNodes[i1].FirstChildDelta = i1 - (mortonCodes.Length + i1); // This delta points into mortonCodes.

                for (int i = i1; i < i2; i++)
                    leaves[i].RightSiblingDelta = 1;
            }
            else
            {
                var i = d < 0 ? i2 : i1;

                var t = delta(mortonCodes, i, i - d) / 3;

                // t is the delta-threshold: All leaves below our internal node must have a delta of less than t to the Morton code at i.

                // Find an outer bound for the opposite end of our child range:
                int dd = 2 * d;
                while (delta(mortonCodes, i, i + dd) / 3 < t)
                    dd *= 2;

                // Now search for the exact position of the opposite end:
                var j = i;
                for (dd /= 2; dd != 0; dd /= 2)
                    if (delta(mortonCodes, i, j + dd) / 3 < t)
                        j += dd;

                if (j < i)
                {
                    var h = i;
                    i = j;
                    j = h;
                }

                // Now determine the child ranges:

                /// <summary>
                /// Returns the index of the last morton code that is more similar to the one at <paramref name="x"/> than the one at <paramref name="y"/>.
                /// </summary>
                int split(int x, int y)
                {
                    var dm = delta(mortonCodes, x, y);
                    int s = x;
                    for (int k = divUp(y - x, 2); true; k = divUp(k, 2))
                    {
                        if (delta(mortonCodes, i, s + k) < dm)
                            s += k;
                        if (t == 1) // If t becomes 1, divUp will always return 1
                            return s;
                    }
                }

                var splits = new int[7];

                splits[3] = split(i, j);
                splits[1] = split(i, splits[3]);
                splits[5] = split(splits[3] + 1, j);
                splits[0] = split(i, splits[1]);
                splits[2] = split(splits[1] + 1, splits[3]);
                splits[4] = split(splits[3] + 1, splits[5]);
                splits[6] = split(splits[5] + 1, j);

                var start = i; // Start of the range for the current child of our internal node
                var pred = - leaves.Length - 1; // Position of the node (leaf or internal!) that represents the previous child of our internal node
                foreach (var end in splits.Distinct())
                {
                    if (pred < leaves.Length) // This is the very first child of our internal node
                    {
                        var p = d > 0 ? i : j; // Is our internal node sitting on the left or on the right end of its morton code range?
                        // If start = end, then the first child is a single morton code and not an internal node.
                        var idx = start < end ? end : end - leaves.Length;
                        internalNodes[p].FirstChildDelta = idx - p;
                        pred = idx;
                    }
                    else // This is not the first child of our internal node
                    {
                        // If start = end, then the current child is a single morton code and not an internal node.
                        var idx = start < end ? start : start - leaves.Length;

                        if (pred < 0) // Previous child is a leaf node
                            leaves[leaves.Length + pred].RightSiblingDelta = idx - pred;
                        else // Previous child is an internal node
                            internalNodes[pred].RightSiblingDelta = idx - pred;

                        pred = idx;
                    }

                    start = end + 1;
                }
            }
        }

        private readonly ImmutableArray<LeafNode> leafNodes;
        private readonly ImmutableArray<InternalNode> internalNodes;

        /// <summary>
        /// Creates a new octree
        /// </summary>
        /// <param name="objectsAndPositions">The objects to be indexed by this tree, along with their positions.</param>
        /// <param name="bounds">The bounds of the space to be covered by the octree. All objects must be contained in this space!</param>
        /// <param name="compress">
        /// Specifies whether the Octree should compress its representation in memory.
        /// Doing so takes time, but requires less memory for most of the lifetime of the octree and
        /// speeds up sequences of accesses to Octree nodes, because caches are used more efficiently.
        /// </param>
        private Octree(IEnumerable<(T, Vector3)> objectsAndPositions, AABB bounds, bool compress=true)
        {
            // Create leave nodes:
            var leaves = objectsAndPositions.Select((op) => new LeafNode(op.Item1, op.Item2)).ToArray();

            // Compute Morton codes for the leave nodes:
            var mcs = leaves.AsParallel().Select(l =>
            {
                var r = (l.Position - bounds.Origin);
                var u = new Vector3(r.X / bounds.Size.X, r.Y / bounds.Size.Y, r.Y / bounds.Size.Y);
                return morton(u);
            }).ToArray();

            // Sort leaves by their Morton codes:
            Array.Sort(mcs, leaves);

            // Create internal nodes of the tree:
            var internals = new InternalNode[leaves.Length];

            Parallel.For(0, internals.Length, (i) => createInternalNode(mcs, i, leaves, internals));

            if (compress)
                throw new NotImplementedException("Compression of octrees has not been implemented yet!");
                // TODO: Compress internal node array. This is done as follows:
                //       1. Recursively go down the tree, until you have found enough nodes.
                //       2. Start one thread per node, that compresses the subarray.
                //          Since all pointers are just deltas and no pointer leaves the subarray that contains it,
                //          threads can independently process their arrays. (This is not completely true, because internal nodes may point to leave nodes)

            this.leafNodes = leaves.ToImmutableArray();
            this.internalNodes = internalNodes.ToImmutableArray();
        }

        /// <summary>
        /// The root node of this octree, representing the whole space.
        /// </summary>
        public IOctreeNode<T> Root
        {
            get
            {
                switch (leafNodes.Length)
                {
                    case 0:
                        throw new IndexOutOfRangeException("This Octree is empty and thus does not have a root node!");
                    case 1:
                        return new OctreeNodeReference(this, -1);
                    default:
                        return new OctreeNodeReference(this, 0);
                }
            }
        }

        /// <summary>
        /// The leaf nodes of this Octree.
        /// </summary>
        protected ImmutableArray<LeafNode> LeafNodes
        {
            get
            {
                return leafNodes;
            }
        }

        /// <summary>
        /// The internal nodes of this Octree.
        /// </summary>
        protected ImmutableArray<InternalNode> InternalNodes
        {
            get
            {
                return InternalNodes;
            }
        }
    }

}
