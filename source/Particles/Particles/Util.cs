using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

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
        public static void ParallelPrefixSum(int[] indicators)
        {
            var offsets = new List<(int, int)>();
            offsets.Add((0, 0));
            Parallel.For(0, indicators.Length, () => (-1, 0), (i, loop, acc) =>
            {
                return (i + 1, acc.Item2 + indicators[i]);
            }, (acc) =>
            {
                lock (offsets) { offsets.Add(acc); }
            });

            offsets.Sort((cs1, cs2) => cs1.Item1.CompareTo(cs2.Item1));

            var count = 0;
            for (int i = 0; i < offsets.Count; i++)
            {
                var o = offsets[i];
                count += o.Item2;
                offsets[i] = (o.Item1, count);
            }

            Parallel.For(0, indicators.Length, () => -1, (i, loop, acc) =>
            {
                if (acc == -1)
                {
                    acc = 0;
                    for (int j = 1; j < offsets.Count; j++)
                        if (i < offsets[j].Item1)
                        {
                            acc = offsets[j - 1].Item2;
                            break;
                        }
                }

                var newAcc = acc + indicators[i];
                indicators[i] = acc;
                return newAcc;
            }, (_) =>{});
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
