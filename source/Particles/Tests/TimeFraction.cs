using System;

namespace Tests
{
    /// <summary>
    /// Comprises information about the time spent on a computation consisting of several steps.
    /// </summary>
    public struct TimeFraction : IEquatable<TimeFraction>
    {
        private readonly double stepCount;
        private readonly double time;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Tests.TimeFraction"/> struct.
        /// </summary>
        /// <param name="time">The total time spent on the computation, in seconds.</param>
        /// <param name="stepCount">The number of computation steps that were executed.</param>
        public TimeFraction(double time, double stepCount=1)
        {
            this.stepCount = stepCount;
            this.time = time;
        }

        /// <summary>
        /// Turns the given step rate into a time fraction.
        /// </summary>
        /// <returns>A time fraction with the given <paramref name="rate"/>.</returns>
        /// <param name="rate">The rate of steps per second (given in Hz)</param>
        public static TimeFraction FromRate(double rate)
        {
            return new TimeFraction(1.0 / rate);
        }

        /// <summary>
        /// The rate at which steps are computed according, in Hz.
        /// </summary>
        public double Rate
        {
            get { return 1.0 / this.TimePerStep; }
        }

        #region "Syntactic Sugar"

        public static TimeFraction operator +(TimeFraction a, TimeFraction b)
        {
            return new TimeFraction(a.time + b.time, a.stepCount + b.stepCount);
        }

        public static TimeFraction operator -(TimeFraction a, TimeFraction b)
        {
            return new TimeFraction(a.time - b.time, a.stepCount - b.stepCount);
        }

        public static TimeFraction operator *(TimeFraction a, double d)
        {
            return new TimeFraction(d * a.time, d * a.stepCount);
        }

        public static TimeFraction operator *(double d, TimeFraction a)
        {
            return new TimeFraction(d * a.time, d * a.stepCount);
        }

        public static TimeFraction operator /(TimeFraction a, double d)
        {
            return new TimeFraction(a.time / d, a.stepCount / d);
        }

        #endregion


        /// <summary>
        /// The number of computation steps that were executed.
        /// </summary>
        public double StepCount
        {
            get
            {
                return stepCount;
            }
        }

        /// <summary>
        /// The total time spent on the computation, in seconds.
        /// </summary>
        public double Time
        {
            get
            {
                return time;
            }
        }

        /// <summary>
        /// The average time per computation step, in seconds.
        /// </summary>
        public double TimePerStep
        {
            get {
                return time / stepCount;
            }
        }

        public bool Equals(TimeFraction other)
        {
            return this.time.Equals(other.time) && this.stepCount.Equals(other.stepCount);
        }

        public override string ToString()
        {
            return string.Format("{0} steps in {1}s", stepCount, time);
        }
    }
}