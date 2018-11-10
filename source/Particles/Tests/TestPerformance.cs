using System;

namespace Tests
{
    /// <summary>
    /// Comprises information about the performance of a simulation test.
    /// </summary>
    public struct TestPerformance
    {
        private readonly TimeFraction simulationTime;
        private readonly TimeFraction renderingTime;
        private readonly double totalTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Tests.TestPerformance"/> struct.
        /// </summary>
        /// <param name="simulationTime">Time spent on simulation itself.</param>
        /// <param name="renderingTime">Time spent on rendering of the simulated behavior.</param>
        /// <param name="totalTime">The time gone by from the start of the simulation until its end, in seconds.</param>
        public TestPerformance(TimeFraction simulationTime, TimeFraction renderingTime, double totalTime)
        {
            this.simulationTime = simulationTime;
            this.renderingTime = renderingTime;
            this.totalTime = totalTime;
        }

        /// <summary>
        /// Information about the time spent on simulation itself.
        /// </summary>
        public TimeFraction SimulationTime
        {
            get { return simulationTime; }
        }
        /// <summary>
        /// Information about the time spent on rendering.
        /// </summary>
        public TimeFraction RenderingTime
        {
            get { return renderingTime; }
        }

        /// <summary>
        /// The total time spent on a test case, in seconds.
        /// </summary>
        /// <remarks>
        /// The total time contains not only simulation and rendering, but also further bookkeeping compuation.
        /// </remarks>
        public double TotalTime
        {
            get { return totalTime; }
        }

        /// <summary>
        /// Combine this simulation performance with the performance of a continuation of the simulation.
        /// </summary>
        /// <returns>The combined simulation performance.</returns>
        /// <param name="other">Information of the performance of the continuation of this simulation.</param>
        public TestPerformance Add(TestPerformance other)
        {
            return new TestPerformance(this.simulationTime + other.simulationTime, this.renderingTime + other.renderingTime, this.totalTime + other.totalTime);
        }

        /// <summary>
        /// Adds information about the time spent on further simulation steps to this simulation performance.
        /// </summary>
        /// <returns>The updated simulation performance, including the additional steps.</returns>
        /// <param name="tf">Information about additional simulation steps.</param>
        public TestPerformance AddSimulationSteps(TimeFraction tf)
        {
            return new TestPerformance(this.simulationTime + tf, this.renderingTime, this.totalTime);
        }

        /// <summary>
        /// Adds information about the time spent on further rendering steps to this simulation performance.
        /// </summary>
        /// <returns>The updated simulation performance, including the additional steps.</returns>
        /// <param name="tf">Information about additional rendering steps.</param>
        public TestPerformance AddRenderingSteps(TimeFraction tf)
        {
            return new TestPerformance(this.simulationTime, this.renderingTime + tf, this.totalTime);
        }

        /// <summary>
        /// Adds information about additional time, spent on neither simulation itself nor rendering, to this simulation performance.
        /// </summary>
        /// <returns>The updated simulation performance, including the additional time.</returns>
        /// <param name="t">The amount of time to add to the total time, in seconds.</param>
        public TestPerformance AddTotalTime(double t)
        {
            return new TestPerformance(this.simulationTime, this.renderingTime, this.totalTime + t);
        }

        public override string ToString()
        {
            return string.Format("{0}sps, {1}rps, {2}s in total", simulationTime.Rate, renderingTime.Rate, totalTime);
        }
    }
}