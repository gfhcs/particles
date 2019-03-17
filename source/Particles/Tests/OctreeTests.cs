using System;
using Xunit;
using Particles;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private void Match<T>(IOctreeNode<T> expected, IOctreeNode<T> actual)
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
                    Match(p.Item1, p.Item2);
            }
        }

        /// <summary>
        /// Asserts that two octrees are completely equal.
        /// </summary>
        /// <param name="expected">An octree. <paramref name="actual"/> is expected to be equal to this one.</param>
        /// <param name="actual">An octree that is expected to be equal to <paramref name="expected"/>.</param>
        /// <typeparam name="T">The type of the items stored in the octrees.</typeparam>
        /// <exception cref="EqualException">If the octrees are not perfectly equal.</exception>
        private void Match<T>(Octree<T> expected, Octree<T> actual)
        {
            if (expected.ItemCount != actual.ItemCount)
                throw new EqualException(expected.ItemCount, actual.ItemCount);
            if (expected.ItemCount > 0)
                Match(expected.Root, actual.Root);
        }

        [Fact()]
        public async Task TestEmpty()
        {
            var bounds = new AABB(new Vector3(0, 0, 0));

            var ot = new Octree<int>(new (int, Vector3)[0], bounds);

            Assert.Throws(typeof(IndexOutOfRangeException), () => ot.Root);

            var cot = await ot.CompressMemory();

            Match(ot, cot);
        }

        [Fact()]
        public async Task TestSingleLeaf()
        {
            var bounds = new AABB(new Vector3(0, 0, 0));

            var ot = new Octree<int>(new[] { (42, new Vector3(0, 0, 0))}, bounds);

            Assert.Equal(1, ot.ItemCount);

            var r = ot.Root;

            Assert.Equal(true, r.IsLeaf);
            Assert.Equal(1, r.Items.Count());
            Assert.Equal((42, new Vector3(0, 0, 0)), r.Items.First());
            Assert.Equal(0, r.Children.Count());

            var cot = await ot.CompressMemory();

            Match(ot, cot);
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
        public async Task TestSingleInternal()
        {

            var positions = new[] { new Vector3(0.5, 0.5, 0.5), new Vector3(0.5, 0.5, 1.5), new Vector3(0.5, 1.5, 0.5), new Vector3(0.5, 1.5, 1.5),
                                    new Vector3(1.5, 0.5, 0.5), new Vector3(1.5, 0.5, 1.5), new Vector3(1.5, 1.5, 0.5), new Vector3(1.5, 1.5, 1.5) };

            var pSet = new HashSet<Vector3>(positions);

            var bounds = new AABB(new Vector3(0, 0, 0), new Vector3(2, 2, 2));

            var ot = new Octree<Vector3>(positions.Zip(positions, (a, b) => (a, b)), bounds);

            Assert.Equal(positions.Length, ot.ItemCount);

            var r = ot.Root;

            Assert.False(r.IsLeaf);
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item1));
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item2));
            Assert.True(pSet.SetEquals(from c in r.Children from p in c.Items select p.Item1));

            var cot = await ot.CompressMemory();

            Match(ot, cot);
        }

        /// <summary>
        /// Computes the height of the given node, i.e. the number of nodes that are visited on the longest
        /// path from the given node to one of the leaves in its subtree. If the given node is a leaf, its height is 1.
        /// </summary>
        /// <param name="node">An octree node.</param>
        /// <typeparam name="T">The type of the octree items.</typeparam>
        private static int height<T>(IOctreeNode<T> node)
        {
            var maxChildHeight = 0;
            foreach (var c in node.Children)
                maxChildHeight = Math.Max(maxChildHeight, height(c));

            return 1 + maxChildHeight;
        }

        /// <summary>
        /// Computes the height of the given octree, i.e. the number of nodes that are visited on the longest path
        /// from the root to one of the leaves. The empty tree has height 0, trees with only one node have height 1.
        /// </summary>
        /// <param name="t">An octree.</param>
        /// <typeparam name="T">The type of the octree items.</typeparam>
        private static int height<T>(Octree<T> t)
        {
            return t.ItemCount == 0 ? 0 : height(t.Root);
        }


        [Fact()]
        public async Task TestMoreInternal1()
        {

            var positions = new[] { new Vector3(0.5, 0.5, 0.5), new Vector3(0.5, 0.6, 0.5), new Vector3(0.5, 1.5, 0.5), new Vector3(0.5, 1.5, 1.5), new Vector3(0.5, 1.6, 1.5), new Vector3(0.4, 1.5, 1.8),
                                    new Vector3(1.5, 0.5, 0.5), new Vector3(1.5, 0.5, 1.5), new Vector3(1.5, 0.5, 1.5),
                                    new Vector3(1.1, 1.5, 0.5), new Vector3(1.2, 1.5, 0.5), new Vector3(1.3, 1.5, 0.1), new Vector3(1.5, 1.4, 0.5), new Vector3(1.5, 1.5, 0.5), new Vector3(1.95, 1.5, 0.75), new Vector3(1.6, 1.1, 0.5), new Vector3(1.3, 1.5, 0.5), new Vector3(1.5, 1.9, 0.85), new Vector3(1.2, 1.9995, 0.59),
                                    new Vector3(1.5, 1.5, 1.5) };

            var pSet = new HashSet<Vector3>(positions);

            var bounds = new AABB(new Vector3(0, 0, 0), new Vector3(2, 2, 2));

            var ot = new Octree<Vector3>(positions.Zip(positions, (a, b) => (a, b)), bounds);

            Assert.Equal(positions.Length, ot.ItemCount);

            var r = ot.Root;

            Assert.False(r.IsLeaf);
            Assert.True(pSet.SetEquals(from p in r.Items select p.Item1));
            Assert.Equal(7, r.Children.Count());

            var childCounts = new HashSet<int>(new[] { 1, 2, 3, 10});
            Assert.True(childCounts.SetEquals(from c in r.Children select c.Items.Count()));
            Assert.Equal(4, height(ot)); // TODO: 4 is just a guess. I don't know if it's correct.

            var cot = await ot.CompressMemory();

            Match(ot, cot);
        }
    }
}
