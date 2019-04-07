using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Particles
{
    public static class Util
    {
        /// <summary>
        /// Computes a running some over an array of integers.
        /// After this call, the i-th cell of <paramref name="indicators"/>
        /// will contain the sum of the values stored in the range [0: i - 1]
        /// before the call.
        /// </summary>
        /// <remarks>
        /// This method efficiently distributes work among the processor cores of
        /// the executing machine.
        /// This method can be used for counting and compacting marked entries of an array,
        /// simply by filling <paramref name="indicators"/> with 0 and 1 entries only.
        /// </remarks>
        /// <param name="indicators">An array of integers.</param>
        /// <param name="index">The first index of the range that is to be transformed into its prefix sum.</param>
        /// <param name="length">The length of the range that is to be transformed into its prefix sum. It will be capped if the range would otherwise exceed the bounds of <paramref name="indicators"/>. </param>
        /// <exception cref="IndexOutOfRangeException">If <paramref name="index"/> is not a valid index for <paramref name="indicators"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="length"/> is negative.</exception>
        public static void ParallelPrefixSum(int[] indicators, int index=0, int length=int.MaxValue)
        {
            if (!(0 <= index && index <= indicators.Length))
                throw new IndexOutOfRangeException(string.Format("{0} must not be negative and at most as large as the length of {1}!", nameof(index), nameof(indicators)));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), string.Format("{0} must not be negative!", nameof(length)));
            length = Math.Min(length, indicators.Length - index);

            void AtomicPrefixSum((int, int, int) range)
            {
                var acc = range.Item2;

                for (int i = range.Item1; i < range.Item3; i++)
                {
                    var x = indicators[i];
                    indicators[i] = acc;
                    acc += x;
                }
            }

            if (length < 512) {
                AtomicPrefixSum((index, 0, length));
                return;
            }

            /// <summary>
            /// Asserts that the given partitioner creates contiguous partitions.
            /// </summary>
            /// <returns>The given partitioner <paramref name="p"/></returns>
            /// <param name="p">a partitioner.</param>
            /// <typeparam name="T">The type of the elements that <paramref name="p"/> partitions.</typeparam>
            OrderablePartitioner<T> assertSanePartitioner<T>(OrderablePartitioner<T> p)
            {
                if (!(p.KeysOrderedInEachPartition && p.KeysNormalized))
                    throw new Exception(string.Format("The .NET implementation yielded an unexpected kind of parallel partitioner! The implementation of {0} is not prepared for this!", nameof(ParallelPrefixSum)));
                return p;
            }

            var ibc = (length + Environment.ProcessorCount) / Environment.ProcessorCount;

            var mtx = new object();
            var blockEnds = new int[ibc]; // Where do blocks start?
            var blockOffsets = new int[ibc]; // By how much should block contents be set off?
            var numBlocks = 0;

            // Loop over the partitions in parallel.
            Parallel.ForEach(assertSanePartitioner(Partitioner.Create(index, length)), (range, _) =>
            {
                var sum = 0;
                for (int i = range.Item1; i < range.Item2; i++)
                    sum += indicators[i];

                lock (mtx) {
                    if (numBlocks == blockEnds.Length)
                    {
                        Array.Resize(ref blockEnds, 2 * blockEnds.Length);
                        Array.Resize(ref blockOffsets, 2 * blockOffsets.Length);
                    }
                    blockEnds[numBlocks] = range.Item2;
                    blockOffsets[numBlocks] = sum;
                    numBlocks++;
                 }
            });

            Array.Sort(blockEnds, blockOffsets, 0, numBlocks);

            ParallelPrefixSum(blockOffsets, 0, numBlocks);

            IEnumerable<(int, int, int)> enumeratePartitions()
            {
                var prevEnd = 0;
                for (int i = 0; i < numBlocks; i++)
                {
                    yield return (prevEnd, blockOffsets[i], blockEnds[i]);
                    prevEnd = blockEnds[i];
                }
            }

            Parallel.ForEach(assertSanePartitioner(Partitioner.Create(enumeratePartitions())), (range, _) => AtomicPrefixSum(range));
        }

        /// <summary>
        /// Computes a prefix count of an array of items, i.e. returns an array
        /// that indicates how many items before a given index satisfy the given predicate.
        /// </summary>
        /// <returns>An array of integers, the i-th cell of which indicates the number of items in <paramref name="items"/>[0:i - 1] that
        /// satisfy the predicate <paramref name="indicator"/>.</returns>
        /// <remarks>
        /// The array returned by this method can be used for compaction, i.e. to project items satisfying <paramref name="indicator"/> into a new array that
        /// then contains all satisfying items and only satisfying items.
        /// </remarks>
        /// <param name="items">An array of items, some of which are to be counted.</param>
        /// <param name="indicator">A predicate returning true for those items that are to be counted.</param>
        /// <typeparam name="T">The type of the items to be counted.</typeparam>
        public static int[] ParallelPrefixCount<T>(T[] items, Func<T, bool> indicator)
        {
            var indicators = new int[items.Length];
            Parallel.For(0, indicators.Length, (i) =>
            {
                indicators[i] = indicator(items[i]) ? 1 : 0;
            });

            ParallelPrefixSum(indicators);
            return indicators;
        }

        /// <summary>
        /// Computes a prefix count of an array of items, i.e. returns an array
        /// that indicates how many items before a given index satisfy the given predicate.
        /// </summary>
        /// <returns>An array of integers, the i-th cell of which indicates the number of items in <paramref name="items"/>[0:i - 1] that
        /// satisfy the predicate <paramref name="indicator"/>.</returns>
        /// <remarks>
        /// The array returned by this method can be used for compaction, i.e. to project items satisfying <paramref name="indicator"/> into a new array that
        /// then contains all satisfying items and only satisfying items.
        /// </remarks>
        /// <param name="items">An array of items, some of which are to be counted.</param>
        /// <param name="indicator">A predicate returning true for those items that are to be counted.</param>
        /// <typeparam name="T">The type of the items to be counted.</typeparam>
        public static int[] ParallelPrefixCount<T>(ImmutableArray<T> items, Func<T, bool> indicator)
        {
            var indicators = new int[items.Length];
            Parallel.For(0, indicators.Length, (i) =>
            {
                indicators[i] = indicator(items[i]) ? 1 : 0;
            });

            ParallelPrefixSum(indicators);
            return indicators;
        }
    }
}
