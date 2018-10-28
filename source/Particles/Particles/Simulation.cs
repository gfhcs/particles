using System;
using System.Threading.Tasks;

namespace Particles
{
    /// <summary>
    /// The process of simulating the development of a physical quantity over time.
    /// </summary>
    /// <typeparam name="Q">The type that represents amounts of the quantity.</typeparam>
    /// <typeparam name="G">The type that represents values of the gradient for the quantity.</typeparam>
    public class Simulation<Q, G> where Q : IDifferentiable<Q, G> where G : IGradient<Q, G>
    {
        private Q state;
        private readonly IIntegrator<Q, G> integrator;
        private double time = 0;
        private readonly double stepSize;

        /// <summary>
        /// Sets up a new simulation.
        /// </summary>
        /// <param name="initialState">The initial state of the quantity.</param>
        /// <param name="integrator">The integrator to be used for simulation steps.</param>
        /// <param name="stepSize">The step size of the simulation. The larger the step size, the less accurate the simulation. The smaller the step size, the faster the simulation.</param>
        public Simulation(Q initialState, IIntegrator<Q, G> integrator, double stepSize)
        {
            this.state = initialState.Copy();
            this.integrator = integrator;
        }

        /// <summary>
        /// The initial state of the quantity.
        /// </summary>
        public IIntegrator<Q, G> Integrator
        {
            get {
                return Integrator;
            }
        }

        /// <summary>
        /// The time up to which the simulation has been computed.
        /// </summary>
        public double Time
        {
            get
            {
                return time;
            }
        }

        /// <summary>
        /// The step size of the simulation.
        /// </summary>
        public double StepSize
        {
            get
            {
                return stepSize;
            }
        }

        /// <summary>
        /// The state up to which the development of the quantity has been simulated.
        /// </summary>
        public Q State
        {
            get{
                return state.Copy();
            }
        }

        /// <summary>
        /// Advances the simulation.
        /// </summary>
        /// <param name="dt">The delta-time by which to advance the simulation. Must be positive.</param>
        public void Advance(double dt)
        {
            if (!(dt > 0.0))
                throw new ArgumentException(string.Format("{0} must be positive!", nameof(dt)));
            
            for (; dt > stepSize; dt -= stepSize)
                integrator.Integrate(state, stepSize);
            integrator.Integrate(state, dt);
        }
    }
}
