using System;
using Xunit;
using Particles;
using System.Linq;

namespace Tests
{
    public class UtilTests
    {
        /// <summary>
        /// Computes the prefix sum of a given array of indicators.
        /// </summary>
        /// <remarks>
        /// This method is supposed to compute the ground truths for testing
        /// <see cref="Util.ParallelPrefixSum(int[])"/>. See there for further
        /// information.
        /// </remarks>
        /// <returns>An array holding the computed prefix sum.</returns>
        /// <param name="indicators">An array of integers. It will not be modified.</param>
        private static int[] PrefixSum(int[] indicators)
        {
            var sum = indicators.Clone() as int[];

            var acc = 0;
            for (int i = 0; i < sum.Length; i++)
            {
                var x = indicators[i];
                sum[i] = acc;
                acc += x;
            }

            return sum;
        }


        [Theory()]
        [InlineData(new int[0])]
        [InlineData(new[] { 0 })]
        [InlineData(new[] { 1 })]
        [InlineData(new[] { 0, 0 })]
        [InlineData(new[] { 0, 1 })]
        [InlineData(new[] { 1, 0 })]
        [InlineData(new[] { 1, 1 })]
        [InlineData(new[] { 1, 0, 0, 1, 1, 0, 0, 1, 0, 1 })]
        [InlineData(new[] { 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1 })]
        public void TestParallelPrefixSumSmall(int[] indicators)
        {
            var groundTruth = PrefixSum(indicators);
            Util.ParallelPrefixSum(indicators);
            Assert.Equal(groundTruth, indicators);
        }

        [Theory()]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        [InlineData(100000000)]
        public void TestParallelPrefixSumLarge(int n)
        {
            var rnd = new Random();

            var indicators = new int[n];
            for (int i = 0; i < indicators.Length; i++)
                indicators[i] = rnd.Next() % 2;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var groundTruth = PrefixSum(indicators);
            sw.Stop();
            var sTime = sw.Elapsed;
            sw.Reset();
            sw.Start();
            Util.ParallelPrefixSum(indicators);
            sw.Stop();
            var mTime = sw.Elapsed;
            //Assert.Equal(groundTruth, indicators);
            Assert.Equal(groundTruth[groundTruth.Length / 2], indicators[indicators.Length / 2]);

            if (Environment.ProcessorCount > 1)
                Assert.True(mTime < sTime, string.Format("Computing prefix sum in parallel ({0}) should have been much faster than on a single core ({1})!", mTime, sTime));
        }
    }
}
