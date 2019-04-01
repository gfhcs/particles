using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Particles
{
    /// <summary>
    /// A tree each node of which represents an axis-aligned bounding box (AABB) in three-dimensional-space.
    /// Each node can have up to 8 children that subdivide the AABB of their parent into smaller AABB's.
    /// Each node contains either at least one out of a universe of objects, or has at least two children.
    /// </summary>
    /// <typeparam name="T">The type of the objects scattered across space.</typeparam>
    public class MortonOctree<T> : ISpatialIndex<MortonOctree<T>.INodeReference, T>
    {
        #region "Morton code"
        private const uint MAX = 2097152; // 2^21

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
            x = ((x << 2) | x) & 0x9249249249249249u;

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
            var x = spread3((uint)Math.Min(Math.Max(v.X * MAX, 0), MAX - 1));
            var y = spread3((uint)Math.Min(Math.Max(v.Y * MAX, 0), MAX - 1));
            var z = spread3((uint)Math.Min(Math.Max(v.Z * MAX, 0), MAX - 1));
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
            x |= x >> 32;
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
        /// Computes the index of the first digit in which two Morton codes differ.
        /// </summary>
        /// <returns>
        /// 0 if Morton codes do not agree on the very first digit.
        /// 64 if Morton codes are perfectly equal.
        /// -1 if <paramref name="i"/> or <paramref name="j"/> are out of bounds for <paramref name="mortonCodes"/>.
        /// </returns>
        /// <remarks>
        /// The return value of the method can be interpreted as a measure for how similar two Morton codes are.
        /// If two Morton codes are very similar, the points identified by them are guaranteed to be close together in 3D space.
        /// If two points are very close together in 3D space, their Morton codes are NOT guaranteed to be similar (although they
        /// might be).
        /// </remarks>
        /// <param name="mortonCodes">An array of Morton codes.</param>
        /// <param name="i">An index into <paramref name="mortonCodes"/>.</param>
        /// <param name="j">An index into <paramref name="mortonCodes"/>.</param>
        private static int sigma(ulong[] mortonCodes, int i, int j)
        {
            try {
                return countLeadingZeros(mortonCodes[i] ^ mortonCodes[j]);
            } catch (IndexOutOfRangeException) {
                return -1;
            }
        }

        /// <summary>
        /// Computes a measure for how similar two Morton codes are.
        /// </summary>
        /// <returns>
        /// -1 if <paramref name="i"/> or <paramref name="j"/> are out of bounds for <paramref name="mortonCodes"/>.
        /// -1 if Morton codes do not agree on the very first of their 64 digits (i.e. the one with index 0)
        /// i if the first digit where Morton codes do not agree is the digit at index 1 + i * 3 + f from the left, where f is either 0, 1 or 2.
        /// </returns>
        /// <remarks>
        /// Morton codes are obtained by reading <paramref name="mortonCodes"/> at indices <paramref name="i"/> and <paramref name="j"/>.
        /// They are supposed to be 64-bit numbers.
        /// If two Morton codes are very similar, the points identified by them are guaranteed to be close together in 3D space.
        /// If two points are very close together in 3D space, their Morton codes are NOT guaranteed to be similar (although they
        /// might be).
        /// </remarks>
        /// <param name="mortonCodes">An array of Morton codes.</param>
        /// <param name="i">An index into <paramref name="mortonCodes"/>.</param>
        /// <param name="j">An index into <paramref name="mortonCodes"/>.</param>
        private static int slottedSigma(ulong[] mortonCodes, int i, int j)
        {
            var s = sigma(mortonCodes, i, j) - 1;
            return s < 0 ? -1 : s / 3;
        }

        /// <summary>
        /// Determines the digit at power <paramref name="power"/> within the binary representation of <paramref name="number"/>.
        /// </summary>
        /// <returns><see langword="true"/>, if the digit is a one, <see langword="false"/> if the digit is a zero.</returns>
        /// <param name="power">An index from the range [0:63].</param>
        /// <param name="number">A number.</param>
        private static bool digit(int power, ulong number)
        {
            return (number >> power) % 2 == 1;
        }

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

            public override string ToString()
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} at {1}", item, position);
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
            /// Indices (not deltas!!!) below zero indicate
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
            /// <remarks>
            /// During construction of the Octree, this value will be set to -1 for all nodes
            /// that are not reachable from the root node. None of the reachable nodes can happen to have
            /// -1 here, because of the following case analysis:
            /// Case 1: The current node does not have a right sibling. In that case this property returns 0.
            /// Case 2: The current node has a right sibling that is an internal node. Then this internal node 
            ///         is guaranteed to have an index greater than the index of the current node and this property
            ///         needs to return a positive value.
            /// Case 3: The current node has a right sibling that is a leaf node. Then this leaf node is guaranteed
            ///         to have a negative index, while the current node has a nonnegative index. Thus this property must
            ///         return a negative value. Assmuing this value to be -1 would mean that the current node would have
            ///         to be the root node of the tree. The root node of the tree however can only return 0 for this property
            ///         because clearly Case 1 applies.
            /// </remarks>
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
        public interface INodeReference : IIndexNode<INodeReference, T>
        {
        }

        /// <summary>
        /// A reference to one of the nodes of an Octree.
        /// </summary>
        protected struct NodeReference : INodeReference
        {
            private readonly MortonOctree<T> owner;
            private readonly int index;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Particles.Octree`1.OctreeNodeReference"/> struct.
            /// </summary>
            /// <param name="owner">The octree into which this reference points.</param>
            /// <param name="index">
            /// The index of the node this reference points to. Positive indices point into <see cref="internalNodes"/>,
            /// while negative indices point into <see cref="leafNodes"/>, where -1 refers to the last element.
            /// </param>
            public NodeReference(MortonOctree<T> owner, int index)
            {
                this.owner = owner;
                this.index = index;
            }

            /// <summary>
            /// The octree into which this reference points.
            /// </summary>
            /// <value>The owner.</value>
            public MortonOctree<T> Owner
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

                        if (delta == -1)
                            throw new Exception("The node this reference points to reports -1 as the offset between itself and its right sibling node in the internal representation of the octree. Nodes that do this never be accessible from outside the octree, so this condition constitutes a bug in the octree implementation!");
                    }
                }
            }

            IEnumerable<INodeReference> ITreeNode<INodeReference>.Children
            {
                get
                {
                    return this.Children.Cast<INodeReference>();
                }
            }

            public IEnumerable<(T, Vector3)> Items
            {
                get
                {
                    var l = this.LastItemIndex;
                    for (int i = this.FirstItemIndex; i <= l; i++)
                    {
                        var leaf = owner.leafNodes[owner.LeafNodes.Length + i];
                        yield return (leaf.Item, leaf.Position);
                    }
                }
            }

            /// <summary>
            /// Returns the index of the first item below the subtree identified by this node reference.
            /// </summary>
            private int FirstItemIndex
            {
                get
                {
                    return IsLeaf ? index : this.Children.First().FirstItemIndex;
                }
            }

            /// <summary>
            /// Returns the index of the last item below the subtree identified by this node reference.
            /// </summary>
            private int LastItemIndex
            {
                get
                {
                    return IsLeaf ? index : this.Children.Last().LastItemIndex;
                }
            }

            public int Arity
            {
                get
                {
                    return this.Children.Count();
                }
            }

            public bool IsLeaf
            {
                get
                {
                    return index < 0;
                }
            }


            public override string ToString()
            {
                return string.Format("Node {0}", index);
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
        /// Finds the index at which morton codes stop having a zero digit in the given position.
        /// </summary>
        /// <returns>
        /// The index of the leftmost morton code in subarray mortonCodes[x:y - 1] that has a one digit at power <paramref name="power"/>. 
        /// If there is no such morton code in that subarray, y is returned.
        /// </returns>
        /// <param name="mortonCodes">A sorted array of Morton codes.</param>
        /// <param name="power">The position in the morton code bit strings where to look for a change from zero to one.</param>
        /// <param name="x">An index into <paramref name="mortonCodes"/> that points to the first item in the subarray of morton codes to be considered.</param>
        /// <param name="y">An index into <paramref name="mortonCodes"/> that points to the first item right of the subarray of morton codes to be considered.</param>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="power"/> is not from [0:63].</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="x"/> is less than or <paramref name="y"/> is greather than the length of <paramref name="mortonCodes"/>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="y"/> &lt; <paramref name="x"/>.</exception>
        private static int split(ulong[] mortonCodes, int power, int x, int y)
        {
            if (!(0 <= power && power <= 63))
                throw new ArgumentOutOfRangeException(string.Format("{0} must be in the range [0:63]!", nameof(power)), nameof(power));
            if (!(0 <= x))
                throw new ArgumentOutOfRangeException(string.Format("{0} must not be negative!", nameof(x)), nameof(x));
            if (!(y <= mortonCodes.Length))
                throw new ArgumentOutOfRangeException(string.Format("{0} must not be greater than the length of {1}!", nameof(y), nameof(mortonCodes)), nameof(x));
            if (y - x < 0)
                throw new ArgumentException(string.Format("{0} must not be less than {1} !", nameof(y), nameof(x)), nameof(y));

            if (y == x || digit(power, mortonCodes[x]))
                return x;

            for (var dd = divUp(y - x, 2); dd != 0; dd = divUp(dd, 2))
            {
                if (x < y - dd /* mortonCodes[x] was already checked above! */ && digit(power, mortonCodes[y - dd]))
                    y -= dd;
                if (dd == 1)
                    break;
            }

            return y;
        }

        /// <summary>
        /// Initializes an internal node of an octree.
        /// </summary>
        /// <remarks>
        /// This method uses the properties of the Z-curve (Morton curve) in order to determine the position of the internal node in the tree.
        /// This method is safe to be called for all leaf indices (<paramref name="i"/>) in parallel. After this method has been called for
        /// all leaf indices, the arras <paramref name="leaves"/> and <paramref name="internalNodes"/> are completely initialized.
        /// </remarks>
        /// <param name="mortonCodes">The sorted array of Morton codes for the objects indexed by the octree. May contain duplicates.</param>
        /// <param name="leaves">The array in which leave nodes for the octree are to be stored.</param>
        /// <param name="i">The index of a leaf node. An internal node with the same index (in <paramref name="internalNodes"/> will be created.</param>
        /// <param name="internalNodes">The array in which internal nodes for the octree are to be stored.</param>
        private static void createInternalNode(ulong[] mortonCodes, int i, LeafNode[] leaves, InternalNode[] internalNodes)
        {
            // Idea:
            // Every morton code sits at the boundary of some morton code range
            // that encompasses one 3D bounding box at a certain scale.
            // We find out the bounds of this box and then make this
            // internal node the root for this box.

            // Step 1: Which of our two neighboring codes is more similar to ours?
            //         If the one on the left is more similar, we are sitting on the right end of a morton code range.
            //         If the one on the right is more similar, we are sitting on the left end of a morton code range.

            var s = Math.Sign(slottedSigma(mortonCodes, i, i + 1) - slottedSigma(mortonCodes, i - 1, i)); // We divide values by three, because the code range of one box spans 3 binary digits.

            // If s == 0, our internal node is neither the first, nor the last child of its parent.

            if (s == 0) // Our left neighbor is as similar to our morton code as our right neighbor.
            {
                // In this case our Morton code must have the same parent node as its left and right neighbor,
                // and it is neither the first, nor the last child of that parent node.
                // In addition, the internal node we have to create here represents exactly one Morton code, 
                // which violates the definition of "internal node" and will be detected by the parent node.
                // Thus we do nothing.
                return;
            }

            // Step 2: Delimit the range of Morton codes that our internal node should represent.
            // We have to initialize an internal node.
            // If s > 0, our internal node sits at the left edge of its leaf range.
            // If s < 0, our internal node sits at the right edge of its leaf range.

            var t = slottedSigma(mortonCodes, i, i - s); // t is the sigma-threshold: All leaves under our internal node must have a slottedSigma of strictly more than t to the Morton code at i.

            // Step 2a): Find an outer bound for the opposite end of our leaf range:
            int dd = 2 * s;
            while (slottedSigma(mortonCodes, i, i + dd) > t)
                dd *= 2;

            // Step 2b): Now search for the exact position of the opposite end:
            var j = i;
            for (dd /= 2; dd != 0; dd /= 2)
                if (slottedSigma(mortonCodes, i, j + dd) > t)
                    j += dd;

            // W.l.o.g let's have i < j:
            var min = Math.Min(i, j);
            var max = Math.Max(i, j);
            i = min;
            j = max;

            j += 1; // j should point to the first code *outside* our range.

            var slotLevel = slottedSigma(mortonCodes, i, j - 1); // The number of leading slots that all morton codes in our range have in common.

            // Special case: All the Morton codes in our leaf range are equal.
            if (slotLevel == 21)
            {
                // Then steps 3 and 4 are replaced by the much simpler task of making *all* the Morton codes in our leaf range direct children of our internal node:

                var idx = s > 0 ? i : j - 1; // Position of our internal node.
                var cidx = i - leaves.Length; // Position of our first leaf node.
                internalNodes[idx].FirstChildDelta = cidx - idx;

                if (s > 0) // Avoids a race: Only the thread creating the internal node at the *left* edge of the leaf range should do this. The other one would write the exact same stuff.
                    for (; cidx < j - leaves.Length - 1; cidx++)
                        leaves[leaves.Length + cidx].RightSiblingDelta = 1;

                return;
            }

            // Step 3: Determine the leaf ranges of the *children* of our internal node:

            var starts = new int[9]; // Tells us where child ranges start. The last entry delimits the end of the last child range.

            var p = 64 - (1 + slotLevel * 3); // All the Morton codes in our range agree from the first digit, i.e. the one at index 63, to the digit at index p (and possible further digits)

            starts[0] = i;
            starts[8] = j;
            starts[4] = split(mortonCodes, p - 1, starts[0], starts[8]);
            starts[2] = split(mortonCodes, p - 2, starts[0], starts[4]);
            starts[6] = split(mortonCodes, p - 2, starts[4], starts[8]);
            starts[1] = split(mortonCodes, p - 3, starts[0], starts[2]);
            starts[3] = split(mortonCodes, p - 3, starts[2], starts[4]);
            starts[5] = split(mortonCodes, p - 3, starts[4], starts[6]);
            starts[7] = split(mortonCodes, p - 3, starts[6], starts[8]);

            // Step 4: Initialize our internal node and its child nodes (in a complementary way):
            // Note: Index zero points at internalNodes[0]. Index -1 points at leafNodes[leafNodes.Length - 1].

            int pred = -leaves.Length - 1; // Position of the node (leaf or internal!) that represents the previous child of our internal node.
            bool first = true;

            for (int si = 0; si < starts.Length - 1; si++)
            {
                var start = starts[si]; // The range for our current child starts here.
                var end = starts[si + 1]; // The range for our current child ends here.

                if (start == end) // The range for our current child is empty.
                    continue;

                if (first) // This is the very first child of our internal node
                {
                    var idx = s > 0 ? i : j - 1; // Position of our internal node.
                    var cidx = end - 1; // Position of our child node. It's at the *end* of its leaf range! (because our internal node might sit at the start!)
                    // If our child node represents only a single morton code, it is a leaf node and not an internal node:
                    if (end - start == 1)
                        cidx -= leaves.Length;
                    internalNodes[idx].FirstChildDelta = cidx - idx;
                    pred = cidx;
                }
                else
                {
                    var cidx = start; // Position of our child node. It's at the *start* of its leaf range! (because our internal node might sit at the end!)

                    if (end - start == 1) // If our child node represents only a single morton code, it is a leaf node and not an internal node: 
                        cidx -= leaves.Length;
                    else
                    {
                        // The current child range is represented by an internal node.
                        // If this child is neither our first, nor our last child, there
                        // are *two* internal nodes that could potentially represent it. 
                        // Since we select only one of the two, the other one must be marked
                        // unreachable.
                        if (end < j)
                            internalNodes[end - 1].RightSiblingDelta = -1;
                    }

                    if (pred < 0) // Previous child is a leaf node
                        leaves[leaves.Length + pred].RightSiblingDelta = cidx - pred;
                    else // Previous child is an internal node
                        internalNodes[pred].RightSiblingDelta = cidx - pred;

                    pred = cidx;
                }
                first = false;
            }
        }

        private readonly ImmutableArray<LeafNode> leafNodes;
        private readonly ImmutableArray<InternalNode> internalNodes;

        /// <summary>
        /// Creates a new Octree.
        /// </summary>
        /// <param name="leafNodes">The leaf nodes of this octree.</param>
        /// <param name="internalNodes">The internal nodes of this octree.</param>
        protected MortonOctree(ImmutableArray<LeafNode> leafNodes, ImmutableArray<InternalNode> internalNodes)
        {
            this.leafNodes = leafNodes;
            this.internalNodes = internalNodes;

            AssertNoSingleInternalChildren();
        }

        /// <summary>
        /// Creates a new octree
        /// </summary>
        /// <param name="objectsAndPositions">The objects to be indexed by this tree, along with their positions.</param>
        /// <param name="bounds">The bounds of the space to be covered by the octree. All objects must be contained in this space!</param>
        public MortonOctree(IEnumerable<(T, Vector3)> objectsAndPositions, AABB bounds)
        {
            // Create leaf nodes:
            var leaves = objectsAndPositions.Select((op) => new LeafNode(op.Item1, op.Item2)).ToArray();

            // Compute Morton codes for the leave nodes:
            var mcs = leaves.AsParallel().Select(l =>
            {
                var r = (l.Position - bounds.Origin);
                var u = new Vector3(r.X / bounds.Size.X, r.Y / bounds.Size.Y, r.Z / bounds.Size.Z);
                return morton(u);
            }).ToArray();

            // Sort leaves by their Morton codes:
            Array.Sort(mcs, leaves);

            // Create internal nodes of the tree:
            var internals = new InternalNode[leaves.Length];

            Parallel.For(0, internals.Length, (i) => createInternalNode(mcs, i, leaves, internals));

            if (internals.Length > 0)
                internals[internals.Length - 1].RightSiblingDelta = -1; // We could use that node as our root, but we don't do so.

            this.leafNodes = leaves.ToImmutableArray();
            this.internalNodes = internals.ToImmutableArray();

            AssertNoSingleInternalChildren();
        }

        /// <summary>
        /// Asserts that this octree does not contain any internal node that is the only child of its parent.
        /// </summary>
        private void AssertNoSingleInternalChildren()
        {
            for (int i = 0; i < internalNodes.Length; i++) {
                var ir = new NodeReference(this, i);
                if (ir.Children.Count() == 1 && !ir.Children.First().IsLeaf)
                    throw new Exception(string.Format("This octree contains an internal node that is the only child of its parent! This constitutes a bug in the implementation of {0}!", nameof(MortonOctree<T>)));
            }
        }

        /// <summary>
        /// The root node of this octree, representing the whole space.
        /// </summary>
        public INodeReference Root
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
        /// Translates one internal node from an array of internal nodes to a new, compacted array.
        /// Only reachable nodes will be copied and nodes will be shifted to the left, according to <paramref name="newIndices"/>, such
        /// that after this method has been called for all indices <paramref name="i"/> the array <paramref name="target"/> will contain precisely all the reachable nodes from <paramref name="source"/>.
        /// Translation involves adjusting deltas stored in the nodes according to <paramref name="newIndices"/>.
        /// </summary>
        /// <param name="source">The array that the internal node is to be read from.</param>
        /// <param name="newIndices">An array that contains the new indices of all the internal nodes.</param>
        /// <param name="i">The index into <paramref name="source"/> specifying the internal node to be translated.</param>
        /// <param name="target">The array that nodes are to be written to.</param>
        private static void shiftInternal(ImmutableArray<InternalNode> source, int[] newIndices, int i, InternalNode[] target)
        {
            if (source[i].RightSiblingDelta == -1)
                return;

            int translate(int idx)
            { 
                return idx < 0 ? idx : newIndices[idx];
            }

            var node = source[i];

            var newIdx = translate(i);

            node.FirstChildDelta = translate(i + node.FirstChildDelta) - newIdx;
            node.RightSiblingDelta = translate(i + node.RightSiblingDelta) - newIdx;

            target[newIdx] = node;
        }

        /// <summary>
        /// Translates one leaf node from an array of leaf nodes to a new one.
        /// The coordinate of the next sibling of this node, which may be an internal node, will be adjusted according to <paramref name="newIndices"/>.
        /// </summary>
        /// <param name="source">The array that the leaf node is to be read from.</param>
        /// <param name="newIndices">An array that contains the new indices of all the internal nodes.</param>
        /// <param name="i">The index into <paramref name="source"/> specifying the leaf node to be translated.</param>
        /// <param name="target">The array that nodes are to be written to.</param>
        private static void shiftLeaf(ImmutableArray<LeafNode> source, int[] newIndices, int i, LeafNode[] target)
        {
            int translate(int idx)
            {
                return idx < 0 ? idx : newIndices[idx];
            }

            var iIdx = i - source.Length;

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
        public MortonOctree<T> CompressMemory()
        {
            if (leafNodes.Length < 2 || leafNodes.Length > internalNodes.Length)
                return this;

            var newIndices = Util.ParallelPrefixCount(internalNodes, (n) => n.RightSiblingDelta != -1);

            var newLeafNodes = new LeafNode[leafNodes.Length];
            var newInternalNodes = new InternalNode[newIndices[newIndices.Length - 1] + 1];

            // Shift elements:
            Parallel.For(0, leafNodes.Length, (i) => shiftLeaf(leafNodes, newIndices, i, newLeafNodes));
            Parallel.For(0, internalNodes.Length, (i) => shiftInternal(internalNodes, newIndices, i, newInternalNodes));

            return new MortonOctree<T>(newLeafNodes.ToImmutableArray(), newInternalNodes.ToImmutableArray());
        }

        #endregion
    }
}
