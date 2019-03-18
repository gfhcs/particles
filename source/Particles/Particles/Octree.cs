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
        /// -1 if Morton codes do not agree on the very first of their 64 digits.
        /// i if the first digit where Morton codes do not agree is the digit at position 1 + i * 3 + f from the left, where f is either 0, 1 or 2.
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
                return string.Format("{0} at {1}", item, position);
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
                        var leaf = owner.leafNodes[owner.LeafNodes.Length + i];
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
                    return IsLeaf ? index : this.Children.Last().LastItemIndex;
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

            if (digit(power, mortonCodes[x]))
                return x;

            for (var dd = divUp(y - x, 2); dd != 0 && x < y - dd; dd = divUp(dd, 2))
            {
                if (digit(power, mortonCodes[y - dd]))
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
        /// <returns>A task object that can be awaited.</returns>
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

            // Special case: All the Morton codes in our leaf range are equal.
            if (mortonCodes[i] == mortonCodes[j - 1])
            {
                // Then steps 3 and 4 are replaced by the much simpler task of making *all* the Morton codes in our leaf range direct children of our internal node:

                var idx = s > 0 ? i : j - 1; // Position of our internal node.
                var cidx = i - leaves.Length; // Position of our first leaf node.
                internalNodes[idx].FirstChildDelta = idx - cidx;

                if (s > 0) // Avoids a race: Only the thread creating the internal node at the *left* edge of the leaf range should do this. The other one would write the exact same stuff.
                    for (; cidx < j - 1; cidx++)
                        leaves[cidx].RightSiblingDelta = 1;

                return;
            }

            // Step 3: Determine the leaf ranges of the *children* of our internal node:

            var starts = new int[9]; // Tells us where child ranges start. The last entry delimits the end of the last child range.

            var p = 64 - (1 + Math.Max(0, t) * 3);

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
                    // If our child node represents only a single morton code, it is a leaf node and not an internal node:
                    if (end - start == 1)
                        cidx -= leaves.Length;

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
        protected Octree(ImmutableArray<LeafNode> leafNodes, ImmutableArray<InternalNode> internalNodes)
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
        public Octree(IEnumerable<(T, Vector3)> objectsAndPositions, AABB bounds)
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
                    throw new Exception(string.Format("This octree contains an internal node that is the only child of its parent! This constitutes a bug in the implementation of {0}!", nameof(Octree<T>)));
            }
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
            var maxPassOn = Math.Max(0, Math.Min(expectedHeight - 3, (int)(Math.Log(Environment.ProcessorCount * Environment.ProcessorCount) / Math.Log(8))));

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
        /// <param name="i">The index into <paramref name="source"/> specifying the leaf node to be translated.</param>
        /// <param name="target">The array that nodes are to be written to.</param>
        private static void shiftLeaf(ImmutableArray<LeafNode> source, int[] chunkShifts, int chunkSize, int[] shifts, int i, LeafNode[] target)
        {
            int translate(int idx)
            {
                return idx < 0 ? idx : chunkShifts[idx / chunkSize] + shifts[idx];
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
                    var startIndex = j;
                    tasks[i] = Task.Run(() => computeShifts(shifts, startIndex, count));
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
                var newInternalNodes = new InternalNode[internalNodes.Length - acc];

                // Shift elements:
                Parallel.For(0, leafNodes.Length, (i) => shiftLeaf(leafNodes, chunkShifts, stride, shifts, i, newLeafNodes));
                Parallel.For(0, internalNodes.Length, (i) => shiftInternal(internalNodes, chunkShifts, stride, shifts, i, newInternalNodes));

                return new Octree<T>(newLeafNodes.ToImmutableArray(), newInternalNodes.ToImmutableArray());
            });
        }

        #endregion
    }
}
