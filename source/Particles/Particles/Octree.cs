using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Collections.Concurrent;

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
        /// For a type implementing  <see cref="IOctreeNode{T}"/> refer to <see cref="NodeReference"/> instead!
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
                get { return position; }
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
        /// For a type implementing  <see cref="IOctreeNode{T}"/> refer to <see cref="NodeReference"/> instead!
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
        protected struct NodeReference : IOctreeNode<T>
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
            public NodeReference(Octree<T> owner, int index)
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

            public IEnumerable<NodeReference> Children
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
                        yield return new NodeReference(owner, idx);
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
            for (; i2 + 1 < mortonCodes.Length && mortonCodes[i2 + 1] == mortonCodes[i1]; i2++) { }

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
        /// Creates a new Octree.
        /// </summary>
        /// <param name="leafNodes">The leaf nodes of this octree.</param>
        /// <param name="internalNodes">The internal nodes of this octree.</param>
        protected Octree(ImmutableArray<LeafNode> leafNodes, ImmutableArray<InternalNode> internalNodes)
        {
            this.leafNodes = leafNodes;
            this.internalNodes = internalNodes;
        }

        /// <summary>
        /// Creates a new octree
        /// </summary>
        /// <param name="objectsAndPositions">The objects to be indexed by this tree, along with their positions.</param>
        /// <param name="bounds">The bounds of the space to be covered by the octree. All objects must be contained in this space!</param>
        public Octree(IEnumerable<(T, Vector3)> objectsAndPositions, AABB bounds)
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
                        return new NodeReference(this, -1);
                    default:
                        return new NodeReference(this, 0);
                }
            }
        }

        /// <summary>
        /// The number of items stored in this octree.
        /// </summary>
        public int ItemCount
        {
            get
            {
                return leafNodes.Length;
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

        #region "Compression"

        /// <summary>
        /// Determines which nodes in the subtree under <paramref name="root"/> are reachable from <paramref name="root"/>.
        /// </summary>
        /// <returns>A computation that writes to <paramref name="shifts"/>.</returns>
        /// <param name="root">An octree node.</param>
        /// <param name="shifts">An array that after this computation indicates which internal nodes are reachable from <paramref name="root"/>: If the internal node at index i is reachable
        /// the cell at index i will be positive. Otherwise the value remains unchanged.</param>
        private static Task reach (NodeReference root, int[] shifts)
        {
            var tasks = new Task[Environment.ProcessorCount];
            var wakeups = new List<TaskCompletionSource<(NodeReference, int)>>();

            /// <summary>
            /// Traverses the subtree under <paramref name="n"/>.
            /// </summary>
            /// <param name="n">An octree node.</param>
            /// <param name="depth">The depth of <paramref name="n"/>, relative to <paramref name="root"/>.</param>
            /// <param name="maxPassOnDepth">The maximum depth up to which a task should check if nodes can be passed to other, waiting tasks.</param>
            void traverse(NodeReference n, int depth, int maxPassOnDepth)
            {
                if (n.IsLeaf)
                    return;

                shifts[n.Index] = 1;

                using (var e = n.Children.GetEnumerator())
                {
                    bool childrenRemaining = e.MoveNext();
                    if (depth <= maxPassOnDepth)
                        lock (wakeups)
                        {
                            while (childrenRemaining && wakeups.Count > 0)
                            {
                                var w = wakeups.Last();
                                wakeups.RemoveAt(wakeups.Count - 1);
                                w.SetResult((e.Current, depth + 1));

                                childrenRemaining = e.MoveNext();
                            }
                        }

                    for (; childrenRemaining; childrenRemaining = e.MoveNext())
                        traverse(e.Current, depth + 1, maxPassOnDepth);
                }
            }

            bool rootAdded = false;

            var expectedHeight = (int)(Math.Log(shifts.Length) / Math.Log(8));
            var maxPassOn = Math.Min(expectedHeight - 3, (int)(Math.Log(Environment.ProcessorCount * Environment.ProcessorCount) / Math.Log(8)));

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    while (true)
                    {
                        var tcs = new TaskCompletionSource<(NodeReference, int)>();

                        lock (wakeups)
                        {
                            if (!rootAdded)
                            {
                                rootAdded = true;
                                tcs.SetResult((root, 0));
                            }
                            else
                                wakeups.Add(tcs);

                            if (wakeups.Count == tasks.Length)
                            {
                                foreach (var w in wakeups)
                                    w.SetCanceled();
                            }
                        }

                        try
                        {
                            var nodeAtDepth = await tcs.Task;
                            traverse(nodeAtDepth.Item1, nodeAtDepth.Item2, maxPassOn);
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                    }
                });
            }

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Computes by how much internal nodes can be shifted to the left based on whether they are actually reachable from the root of the octree.
        /// </summary>
        /// <returns>The number of unreachable nodes in the chunk considered by this call.</returns>
        /// <param name="shifts">
        /// An array that stores 1 for every internal node that is reachable from the root of an octree and 0 for all other internal nodes.
        /// After this call, each cell that represents a reachable node will contain a delta value that indicates by how much the internal
        /// node should be shifted to the left in order to compact the array of internal nodes such that no gaps of unreachable nodes remain.
        /// Cells representing unreachable nodes will receive a negative value.
        /// </param>
        /// <param name="startIndex">The index of the first internal node to be considered by this call. Its shift is guaranteed to be zero if it is reachable, and some negative value otherwise.</param>
        /// <param name="count">The number of internal nodes to be considered by this call. They form contiguous part of the array of internal nodes that starts at <paramref name="startIndex"/>.</param>
        private static int computeShifts(int[] shifts, int startIndex, int count)
        {
            int acc = 0;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                switch (shifts[i])
                {
                    case 0:
                        acc++;
                        shifts[i] = -1;
                        break;
                    case 1:
                        shifts[i] = -acc;
                        break;
                    default:
                        throw new NotImplementedException(string.Format("{0}[{1}] contains the value {2}, which {3} has not been crafted to deal with!", nameof(shifts), i, shifts[i], nameof(computeShifts)));
                }
            }
            return acc;
        }

        /// <summary>
        /// Translates one internal node from an array of internal nodes to a new, compacted array.
        /// Only reachable nodes will be copied and nodes will be shifted to the left, according to <paramref name="chunkShifts"/> and <paramref name="shifts"/>, such
        /// that after this method has been called for all indices <paramref name="i"/> the array <paramref name="target"/> will contain precisely all the reachable nodes from <paramref name="source"/>.
        /// Translation involves adjusting indices stored in the nodes according to <paramref name="chunkShifts"/> and <paramref name="shifts"/>.
        /// </summary>
        /// <param name="source">The array that the internal node is to be read from.</param>
        /// <param name="chunkShifts">An array that holds one shift value for each chunk of <paramref name="source"/>.
        /// A chunk is a contiguous part of <paramref name="source"/>. All chunks except for the last one must have length <paramref name="chunkSize"/>.
        /// The shift value of a chunk indicates by how much the last internal node of that chunk is to be shifted to the left, globally; i.e. this shift value
        /// may specify that the last item is to be moved outside of its original chunk.</param>
        /// <param name="chunkSize">The number of internal nodes per chunk. All chunks of <paramref name="source"/> have this length, except for the very last one.</param>
        /// <param name="shifts">An array that specifies by how much each internal node in <paramref name="source"/> is to be shifted to the left, *within* its chunk; i.e. the very first node in each chunk can only have value 0, if it is reachable. Unreachable nodes must bear a negative value!</param>
        /// <param name="i">The index into <paramref name="source"/> specifying the internal node to be translated.</param>
        /// <param name="target">The array that nodes are to be written to.</param>
        private static void shiftInternal(ImmutableArray<InternalNode> source, int[] chunkShifts, int chunkSize, int[] shifts, int i, InternalNode[] target)
        {
            if (shifts[i] < 0)
                return;

            int translate(int idx)
            { 
                return idx < 0 ? idx : chunkShifts[idx / chunkSize] + shifts[idx];
            }

            var node = source[i];

            var newIdx = translate(i);

            node.FirstChildDelta = translate(i + node.FirstChildDelta) - newIdx;
            node.RightSiblingDelta = translate(i + node.RightSiblingDelta) - newIdx;

            target[newIdx] = node;
        }

        /// <summary>
        /// Translates one leaf node from an array of leaf nodes to a new one.
        /// The coordinate of the next sibling of this node, which may be an internal node, will be shifted according to <paramref name="chunkShifts"/> and <paramref name="shifts"/>.
        /// </summary>
        /// <param name="source">The array that the leaf node is to be read from.</param>
        /// <param name="chunkShifts">An array that holds one shift value for each chunk of the internal nodes that <paramref name="source"/> belongs to.
        /// A chunk is a contiguous part of that internal node array. All chunks except for the last one must have length <paramref name="chunkSize"/>.
        /// The shift value of a chunk indicates by how much the last internal node of that chunk is to be shifted to the left, globally; i.e. this shift value
        /// may specify that the last item is to be moved outside of its original chunk.</param>
        /// <param name="chunkSize">The number of internal nodes per chunk. All chunks have this length, except for the very last one.</param>
        /// <param name="shifts">An array that specifies by how much each internal node is to be shifted to the left, *within* its chunk; i.e. the very first node in each chunk can only have value 0, if it is reachable. Unreachable nodes must bear a negative value!</param>
        /// <param name="i">The index into <paramref name="source"/> specifying the internal node to be translated.</param>
        /// <param name="target">The array that nodes are to be written to.</param>
        private static void shiftLeaf(ImmutableArray<LeafNode> source, int[] chunkShifts, int chunkSize, int[] shifts, int i, LeafNode[] target)
        {
            int translate(int idx)
            {
                return idx < 0 ? idx : chunkShifts[idx / chunkSize] + shifts[idx];
            }

            var iIdx = source.Length - i;

            var node = source[i];

            node.RightSiblingDelta = translate(iIdx + node.RightSiblingDelta) - iIdx;

            target[i] = node;
        }

        /// <summary>
        /// Returns an equivalent octree the memory footprint of which is smaller.
        /// </summary>
        /// <remarks>
        /// Compressing memory takes time, but reduces the amount of memory occupied by the octree.
        /// Furthermore, compressed octrees usually exhibit faster node accesses, because caches are used more efficiently.
        /// </remarks>
        public Task<Octree<T>> CompressMemory()
        {
            return Task.Run(async () =>
            {
                if (leafNodes.Length < 2 || leafNodes.Length > internalNodes.Length)
                    return this;

                var shifts = new int[internalNodes.Length];

                // Find reachable nodes:
                await reach(new NodeReference(this, 0), shifts);

                // Compute deltas by which elements have to be shifted *within each chunk*:
                var tasks = new Task<int>[Environment.ProcessorCount];
                var stride = divUp(internalNodes.Length, tasks.Length);
                var j = 0;
                for (int i = 0; i < tasks.Length; i++)
                {
                    var count = Math.Min(stride, internalNodes.Length - j);
                    tasks[i] = Task.Run(() => computeShifts(shifts, j, count));
                    j += count;
                }

                var chunkShifts = await Task.WhenAll(tasks);

                var acc = 0;
                for (int i = 0; i < chunkShifts.Length; i++)
                {
                    var h = chunkShifts[i];
                    chunkShifts[i] = acc;
                    acc += h;
                }

                var newLeafNodes = new LeafNode[leafNodes.Length];
                var newInternalNodes = new InternalNode[internalNodes.Length - chunkShifts.Last()];

                // Shift elements:
                Parallel.For(0, leafNodes.Length, (i) => shiftLeaf(leafNodes, chunkShifts, stride, shifts, i, newLeafNodes));
                Parallel.For(0, internalNodes.Length, (i) => shiftInternal(internalNodes, chunkShifts, stride, shifts, i, newInternalNodes));

                return new Octree<T>(newLeafNodes.ToImmutableArray(), newInternalNodes.ToImmutableArray());
            });
        }

        #endregion
    }
}
