using System;
using System.Collections.Immutable;
using System.Linq;

namespace Particles
{
    /// <summary>
    /// A mathematical function that is convoluted with a physical signal over the time domain,
    /// in order to filter the signal.
    /// </summary>
    public class DiscreteFilter
    {
        private readonly double width;
        private readonly double delta;
        private readonly double n;
        private readonly ImmutableArray<double> kernel;
        private Func<double, double> argument;

        /// <summary>
        /// Creates a new discrete filter.
        /// </summary>
        /// <param name="width">The width of the convolution kernel.</param>
        /// <param name="kernel">A discrete representation of the convolution kernel. Its length must be odd.</param>
        /// <param name="argument">The physical signal to filter.</param>
        protected DiscreteFilter(double width, ImmutableArray<double> kernel, Func<double, double> argument)
        {
            if (kernel.Length % 2 != 1)
                throw new ArgumentException("The length of the kernel must be odd!", nameof(kernel));
            this.width = width;
            this.kernel = kernel;
            this.argument = argument;
            this.delta = width / (Math.Max(1, kernel.Length - 1));
            this.n = kernel.Sum();
        }

        /// <summary>
        /// Evaluates the convolution of this filter with its argument signal.
        /// </summary>
        /// <returns>The value of the convolution for the given <paramref name="x"/>.</returns>
        /// <param name="x">A point at which the argument signal is defined.</param>
        public double Evaluate(double x)
        {
            var e = 0.0;
            for (int i = 0; i < kernel.Length; i++)
                e += kernel[i] * argument(x + (i - kernel.Length / 2) * delta);

            return e / n;
        }
    }

}
