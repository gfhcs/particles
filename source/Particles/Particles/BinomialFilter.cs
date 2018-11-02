using System;
using System.Collections.Immutable;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// Represents the convolution of a binomial filter with a physical signal.
    /// This is an approximation of a Gaussian filter.
    /// </summary>
    public class BinomialFilter : DiscreteFilter
    {
        /// <summary>
        /// Returns the <paramref name="n"/>-th line of Pascal's triangle.
        /// </summary>
        /// <param name="n">The 1-based index of the line of Pascal's triangle that is to be computed. Must be odd.</param>
        private static double[] pascal(int n)
        {
            if (n % 2 != 1)
                throw new ArgumentException(string.Format("{0} must be odd!", nameof(n)), nameof(n));

            var line = new double[n];

            var m = n / 2;
            line[m] = 1;

            for (; n > 0; n--)
                for (int i = 1; i < m; i++) {
                    var s = line[m + i - 1] + line[m + i];
                    line[m + i] = s;
                    line[m - 1] = s;
            }

            return line;
        }

        /// <summary>
        /// Creates a new binomial filter.
        /// </summary>
        /// <param name="width">The width of the convolution kernel.</param>
        /// <param name="precision">The precision with which a gaussian kernel is to be approximated. Must be positive. Higher values give more precision, but require more computation.</param>
        /// <param name="argument">The physical signal to filter.</param>
        public BinomialFilter(double width, int precision, Func<double, double> argument) : base(width, pascal(precision + (1 - precision % 2)).ToImmutableArray(), argument)
        {
        }
    }
}
