using System;
using Xunit;
using Particles;
using System.Linq;
using System.Collections.Generic;
using Xunit.Sdk;

namespace Tests
{
    public class OctreeTests
    {
        /// <summary>
        /// Asserts that the subtrees under two octree nodes are perfectly equal.
        /// </summary>
        /// <param name="expected">An octree node. The subtree under it is expected to be equal to the one under <paramref name="actual"/></param>
        /// <param name="actual">An octree node the subtree under which is expected to be equal to the one under <paramref name="expected"/>.</param>
        /// <typeparam name="T">The type of the items stored in the octrees.</typeparam>
        /// <exception cref="EqualException">If the subtrees are not perfectly equal.</exception>
        private void Match<N, T>(N expected, N actual) where N : IIndexNode<N, T>
        {
            if (expected.IsLeaf)
            {
                if (!actual.IsLeaf)
                    throw new EqualException(expected, actual);

                Assert.Equal(expected.Items.Count(), actual.Items.Count());

                foreach (var p in expected.Items.Zip(actual.Items, (i, j) => (i, j)))
                    Assert.Equal(p.Item1, p.Item2);
            }
            else
            {
                if (actual.IsLeaf)
                    throw new EqualException(expected, actual);

                Assert.Equal(expected.Children.Count(), actual.Children.Count());

                foreach (var p in expected.Children.Zip(actual.Children, (i, j) => (i, j)))
                    Match<N, T>(p.Item1, p.Item2);
            }
        }

        /// <summary>
        /// Asserts that two octrees are completely equal.
        /// </summary>
        /// <param name="expected">An octree. <paramref name="actual"/> is expected to be equal to this one.</param>
        /// <param name="actual">An octree that is expected to be equal to <paramref name="expected"/>.</param>
        /// <typeparam name="T">The type of the items stored in the octrees.</typeparam>
        /// <exception cref="EqualException">If the octrees are not perfectly equal.</exception>
        private void Match<T>(MortonOctree<T> expected, MortonOctree<T> actual)
        {
            if (expected.ItemCount != actual.ItemCount)
                throw new EqualException(expected.ItemCount, actual.ItemCount);
            if (expected.ItemCount > 0)
                Match<MortonOctree<T>.INodeReference, T>(expected.Root, actual.Root);
        }

        /// <summary>
        /// Asserts that the given <paramref name="tree"/>  satisfies all the invariants of an octree.
        /// </summary>
        /// <param name="tree">An octree.</param>
        /// <typeparam name="T">The type of the items stored in <paramref name="tree"/>.</typeparam>
        private void AssertOctreeInvariants<T>(MortonOctree<T> tree)
        {
            AABB visit(MortonOctree<T>.INodeReference node)
            {
                var cc = node.Arity;

                if (cc == 0)
                {
                    Assert.True(node.IsLeaf, "A node that reported to have arity zero did not report to be a leaf node!");
                    Assert.True(node.IsLeaf(), "A node that reported to have arity zero did not report to be a leaf node!");
                    Assert.Equal(0, node.Children.Count());

                    Assert.True(node.Items.Any(), "A leaf node that does not contain any items was found!");

                    return (from p in node.Items select new AABB(p.Item2)).Aggregate(AABB.Bound);
                }
                else
                {
                    Assert.False(node.IsLeaf, string.Format("A node that reported to have arity {0} reported to be a leaf node!", cc));
                    Assert.False(node.IsLeaf(), string.Format("A node that reported to have arity {0} reported to be a leaf node!", cc));
                    Assert.True(cc <= 8, "A node with more than 8 children was found!");
                    Assert.True(2 <= cc, "A node with only a single child was found!");

                    var childBoxes = (from c in node.Children select visit(c)).ToArray();

                    Assert.Equal(cc, childBoxes.Length);

                    var childUnion = childBoxes.Aggregate(AABB.Bound);

                    var parentBox = (from p in node.Items select new AABB(p.Item2)).Aggregate(AABB.Bound);

                    // Partent box and smallest upper bound of the child boxes should be the same:
                    var dox = parentBox.Origin.X - childUnion.Origin.X;
                    var doy = parentBox.Origin.Y - childUnion.Origin.Y;
                    var doz = parentBox.Origin.Z - childUnion.Origin.Z;
                    var dsx = parentBox.Size.X - childUnion.Size.X;
                    var dsy = parentBox.Size.Y - childUnion.Size.Y;
                    var dsz = parentBox.Size.Z - childUnion.Size.Z;

                    Assert.True(Math.Abs(dox * doy * doz) < 1E-300 && Math.Abs(dsx * dsy * dsz) < 1E-300, "A node was found where the smallest AABB containing all its items is equal to smallest AABB containing all the child boxes!");

                    for (int i = 0; i < childBoxes.Length; i++)
                        for (int j = 0; j < childBoxes.Length; j++)
                            if (i != j)
                            {
                                var box1 = childBoxes[i];
                                var box2 = childBoxes[j];
                                var x = AABB.Intersect(box1, box2);

                                Assert.True(x.IsEmpty, string.Format("A node with two intersecting child boxes was found: The intersection of {0} and {1} is {2}", box1, box2, x));
                            }

                    return parentBox;
                }
            }

            visit(tree.Root);
        }

        [Fact()]
        public void TestEmpty()
        {
            var bounds = new AABB(new Vector3(0, 0, 0));

            var ot = new MortonOctree<int>(new (int, Vector3)[0], bounds);

            Assert.Throws(typeof(IndexOutOfRangeException), () => ot.Root);

            Match(ot, ot.CompressMemory());
        }

        [Fact()]
        public void TestSingleLeaf()
        {
            var bounds = new AABB(new Vector3(0, 0, 0));

            var ot = new MortonOctree<int>(new[] { (42, new Vector3(0, 0, 0))}, bounds);

            Assert.Equal(1, ot.ItemCount);

            var r = ot.Root;

            Assert.Equal(true, r.IsLeaf);
            Assert.Equal(1, r.Items.Count());
            Assert.Equal((42, new Vector3(0, 0, 0)), r.Items.First());
            Assert.Equal(0, r.Children.Count());

            Match(ot, ot.CompressMemory());
        }

        /// <summary>
        /// Tests the creation of an Octree with only one single internal node.
        /// </summary>
        /// <remarks>
        /// The Morton codes for the positions in this test are (sorted):
        /// 00001110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 00011110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 00101110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 00111110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 01001110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 01011110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 01101110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// 01111110 00000000 00000000 00000000 00000000 00000000 00000000 00000000
        /// </remarks>
        [Fact()]
        public void TestSingleInternal()
        {
            var positions = new[] { new Vector3(0.5, 0.5, 0.5), new Vector3(0.5, 0.5, 1.5), new Vector3(0.5, 1.5, 0.5), new Vector3(0.5, 1.5, 1.5),
                                    new Vector3(1.5, 0.5, 0.5), new Vector3(1.5, 0.5, 1.5), new Vector3(1.5, 1.5, 0.5), new Vector3(1.5, 1.5, 1.5) };

            var pSet = new HashSet<Vector3>(positions);

            var bounds = new AABB(new Vector3(0, 0, 0), new Vector3(2, 2, 2));

            var ot = new MortonOctree<Vector3>(positions.Zip(positions, (a, b) => (a, b)), bounds);

            Assert.Equal(positions.Length, ot.ItemCount);

            var r = ot.Root;

            Assert.False(r.IsLeaf);
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item1));
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item2));
            Assert.True(pSet.SetEquals(from c in r.Children from p in c.Items select p.Item1));

            AssertOctreeInvariants(ot);

            Match(ot, ot.CompressMemory());
        }

        /// <summary>
        /// Tests the creation of an Octree with more than one internal node.
        /// </summary>
        /// <remarks>
        /// The Morton codes for the positions in this test are (sorted):
        ///  0 0000111000000000000000000000000000000000000000000000000000000000
        ///  1 0000111000000010010000000010010000000010010000000010010000000010
        ///  2 0010111000000000000000000000000000000000000000000000000000000000
        ///  3 0011011101100000001101100000001101100000001101100000001101100000
        ///  4 0011111000000000000000000000000000000000000000000000000000000000
        ///  5 0011111000000010010000000010010000000010010000000010010000000010
        ///  6 0100111000000000000000000000000000000000000000000000000000000000
        ///  7 0101111000000000000000000000000000000000000000000000000000000000
        ///  8 0101111000000000000000000000000000000000000000000000000000000000
        ///  9 0110010100000001101100000001101100000001101100000001101100000001
        /// 10 0110011000000100100000000100100000000100100000000100100000000100
        /// 11 0110011000100100000000100100000000100100000000100100000000100100
        /// 12 0110011010110111010011111111010010100110011010111110000000111111
        /// 13 0110011100000000100100000000100100000000100100000000100100000000
        /// 14 0110101000000110110000000110110000000110110000000110110000000110
        /// 15 0110101010010000000010010000000010010000000010010000000010010000
        /// 16 0110111000000000000000000000000000000000000000000000000000000000
        /// 17 0110111011010001001010010001001010010001001010010001001010010001
        /// 18 0110111101100100000000100100000000100100000000100100000000100100
        /// 19 0111111000000000000000000000000000000000000000000000000000000000
        /// </remarks>
        [Fact()]
        public void TestMoreInternal1()
        {

            var positions = new[] { new Vector3(0.5, 0.5, 0.5), new Vector3(0.5, 0.6, 0.5), new Vector3(0.5, 1.5, 0.5), new Vector3(0.5, 1.5, 1.5), new Vector3(0.5, 1.6, 1.5), new Vector3(0.4, 1.5, 1.8),
                                    new Vector3(1.5, 0.5, 0.5), new Vector3(1.5, 0.5, 1.5), new Vector3(1.5, 0.5, 1.5),
                                    new Vector3(1.1, 1.5, 0.5), new Vector3(1.2, 1.5, 0.5), new Vector3(1.3, 1.5, 0.1), new Vector3(1.5, 1.4, 0.5), new Vector3(1.5, 1.5, 0.5), new Vector3(1.95, 1.5, 0.75), new Vector3(1.6, 1.1, 0.5), new Vector3(1.3, 1.5, 0.5), new Vector3(1.5, 1.9, 0.85), new Vector3(1.2, 1.9995, 0.59),
                                    new Vector3(1.5, 1.5, 1.5) };

            var pSet = new HashSet<Vector3>(positions);

            var bounds = new AABB(new Vector3(0, 0, 0), new Vector3(2, 2, 2));

            var ot = new MortonOctree<Vector3>(positions.Zip(positions, (a, b) => (a, b)), bounds);

            Assert.Equal(positions.Length, ot.ItemCount);

            var r = ot.Root;

            Assert.False(r.IsLeaf);
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item1));
            Assert.Equal(5, ot.Height());

            var layer1 = r.Children.ToArray();
            var layer2 = (from n in layer1 from c in n.Children select c).ToArray();
            var layer3 = (from n in layer2 from c in n.Children select c).ToArray();
            var layer4 = (from n in layer3 from c in n.Children select c).ToArray();
            var layer5 = (from n in layer4 from c in n.Children select c).ToArray();
            var layer6 = (from n in layer4 from c in n.Children select c).ToArray();

            Assert.Equal(7, layer1.Length);
            Assert.Equal(3, layer1.Count((n) => n.IsLeaf));
            Assert.Equal(10, layer2.Length);
            Assert.Equal(6, layer2.Count((n) => n.IsLeaf));
            Assert.Equal(10, layer3.Length);
            Assert.Equal(9, layer3.Count((n) => n.IsLeaf));
            Assert.Equal(2, layer4.Length);
            Assert.Equal(2, layer4.Count((n) => n.IsLeaf));
            Assert.Equal(0, layer5.Length);

            AssertOctreeInvariants(ot);

            Match(ot, ot.CompressMemory());
        }

        [Theory()]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public void TestRandom(int n)
        {
            // Create objects at random locations in [0, 1]^3 : 
            var positions = new Vector3[n];
            var rnd = new Random();
            for (int i = 0; i < n; i++)
                positions[i] = new Vector3(rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble());

            var pSet = new HashSet<Vector3>(positions);

            var bounds = new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            var ot = new MortonOctree<Vector3>(positions.Zip(positions, (a, b) => (a, b)), bounds);

            Assert.Equal(positions.Length, ot.ItemCount);

            var r = ot.Root;

            Assert.False(r.IsLeaf);
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item1));

            AssertOctreeInvariants(ot);

            Match(ot, ot.CompressMemory());
        }
    }
}
