using System;
namespace Particles
{
    /// <summary>
    /// A cloud of point masses that attract each other.
    /// </summary>
    public class MatterCloud : IDifferentiable<MatterCloud, MatterCloudGradient>
    {
        private readonly double[] masses;
        private readonly Vector3[] positions;
        private readonly Vector3[] velocities;

        /// <summary>
        /// Creates a new matter cloud.
        /// </summary>
        /// <param name="n">The number of particles in the cloud.</param>
        public MatterCloud(int n)
        {
            this.positions = new Vector3[n];
            this.velocities = new Vector3[n];
            this.masses = new double[n];
        }

        /// <summary>
        /// At index i this array contains the position of the i-th point mass.
        /// </summary>
        public Vector3[] Positions
        {
            get { return positions; }
        }

        /// <summary>
        /// At index i this array contains the velocity of the i-th point mass.
        /// </summary>
        public Vector3[] Velocities
        {
            get { return velocities; }
        }

        /// <summary>
        /// At index i this array contains the mass of the i-th point mass.
        /// </summary>
        public double[] Masses
        {
            get { return masses; }
        }

        public void Add(double f, MatterCloud b)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += f * b.positions[i];
                velocities[i] += f * b.velocities[i];
                masses[i] += f * b.masses[i];
            }
        }

        public void Add(MatterCloudGradient g, double dt)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += dt * g.Velocities[i];
                velocities[i] += dt * g.Accelerations[i];
                masses[i] += dt * g.MassRates[i];
            }
        }

        public MatterCloud Copy()
        {
            var n = positions.Length;
            var copy = new MatterCloud(n);
            Array.Copy(this.positions, copy.positions, n);
            Array.Copy(this.velocities, copy.velocities, n);
            Array.Copy(this.masses, copy.masses, n);
            return copy;
        }

        public MatterCloudGradient Differentiate(double dt, MatterCloud q)
        {
            return new MatterCloudGradient(this, dt, q);
        }

        public MatterCloudGradient GetGradient()
        {
            return new MatterCloudGradient(this);
        }

    }

    /// <summary>
    /// The gradient of a matter cloud.
    /// </summary>
    public class MatterCloudGradient : IGradient<MatterCloud, MatterCloudGradient>
    {
        private readonly double[] dm;
        private readonly Vector3[] velocities;
        private readonly Vector3[] accelerations;

        /// <summary>
        /// The gravitational constant.
        /// </summary>
        public const double G = 6.67408E-11;

        /// <summary>
        /// Creates a new matter cloud gradient.
        /// </summary>
        /// <param name="n">The number of particles in the cloud.</param>
        public MatterCloudGradient(int n)
        {
            this.velocities = new Vector3[n];
            this.accelerations = new Vector3[n];
            this.dm = new double[n];
        }

        public MatterCloudGradient(MatterCloud c) : this(c.Positions.Length)
        {
            int n = c.Positions.Length;

            // dm is already filled with zeros.
            Array.Copy(c.Velocities, this.velocities, n);

            // Compute graviational accelerations:
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) {
                    if (i == j) continue;

                    var r = c.Positions[j] - c.Positions[i];

                    accelerations[i] += (r / r.Magnitude) * G * c.Masses[j] / (r * r);
                }
        }

        public MatterCloudGradient(MatterCloud a, double dt, MatterCloud b) : this(a.Positions.Length)
        {
            for (int i = 0; i < a.Positions.Length; i++)
            {
                this.velocities[i] = (b.Positions[i] - a.Positions[i]) / dt;
                this.accelerations[i] = (b.Velocities[i] - a.Velocities[i]) / dt;
                this.dm[i] += (b.Masses[i] - a.Masses[i]) / dt;
            }
        }

        /// <summary>
        /// At index i this array contains the velocity of the i-th point mass.
        /// </summary>
        public Vector3[] Velocities
        {
            get { return velocities; }
        }

        /// <summary>
        /// At index i this array contains the acceleration affecting the i-th point mass.
        /// </summary>
        public Vector3[] Accelerations
        {
            get { return accelerations; }
        }

        /// <summary>
        /// At index i this array contains the rate at which the i-th point mass is gaining in mass.
        /// </summary>
        public double[] MassRates
        {
            get { return dm; }
        }

        public void Add(MatterCloud q, double dt)
        {
            q.Add(this, dt);
        }

        public void Add(double f, MatterCloudGradient b)
        {
            for (int i = 0; i < velocities.Length; i++)
            {
                accelerations[i] += f * b.accelerations[i];
                velocities[i] += f * b.velocities[i];
                dm[i] += f * b.dm[i];
            }
        }

        public MatterCloudGradient Copy()
        {
            var n = velocities.Length;
            var copy = new MatterCloudGradient(n);
            Array.Copy(this.accelerations, copy.accelerations, n);
            Array.Copy(this.velocities, copy.velocities, n);
            Array.Copy(this.dm, copy.dm, n);
            return copy;
        }
    }
}
