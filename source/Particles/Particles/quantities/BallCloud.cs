using System;
namespace Particles
{
    /// <summary>
    /// A cloud of ball masses that attract each other.
    /// </summary>
    public class BallCloud : IDifferentiable<BallCloud, BallCloudGradient>
    {
        private readonly double[] masses;
        private readonly double[] radii;
        private readonly Vector3[] positions;
        private readonly Vector3[] velocities;

        /// <summary>
        /// Creates a new matter cloud.
        /// </summary>
        /// <param name="n">The number of particles in the cloud.</param>
        public BallCloud(int n)
        {
            this.positions = new Vector3[n];
            this.radii = new double[n];
            this.velocities = new Vector3[n];
            this.masses = new double[n];
        }

        /// <summary>
        /// At index i this array contains the position of the i-th ball mass.
        /// </summary>
        public Vector3[] Positions
        {
            get { return positions; }
        }     

        /// <summary>
        /// At index i this array contains the radius of the i-th ball mass.
        /// </summary>
        public double[] Radii
        {
            get { return radii; }
        }

        /// <summary>
        /// At index i this array contains the velocity of the i-th ball mass.
        /// </summary>
        public Vector3[] Velocities
        {
            get { return velocities; }
        }

        /// <summary>
        /// At index i this array contains the mass of the i-th ball mass.
        /// </summary>
        public double[] Masses
        {
            get { return masses; }
        }

        public void Add(double f, BallCloud b)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += f * b.positions[i];
                var r1 = radii[i];
                var r2 = f * b.radii[i];
                radii[i] = Math.Pow(r1 * r1 * r1 + r2 * r2 * r2, 1.0 / 3.0);
                velocities[i] += f * b.velocities[i];
                masses[i] += f * b.masses[i];
            }
        }

        public void Add(BallCloudGradient g, double dt)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += dt * g.Velocities[i];
                radii[i] += dt * Math.Pow(0.75 * g.VolumeRates[i] / Math.PI, 1.0 / 3.0);
                velocities[i] += dt * g.Accelerations[i];
                masses[i] += dt * g.MassRates[i];
            }
        }

        public BallCloud Copy()
        {
            var n = positions.Length;
            var copy = new BallCloud(n);
            Array.Copy(this.positions, copy.positions, n);
            Array.Copy(this.radii, copy.radii, n);
            Array.Copy(this.velocities, copy.velocities, n);
            Array.Copy(this.masses, copy.masses, n);
            return copy;
        }

        public BallCloudGradient Differentiate(double dt, BallCloud q)
        {
            return new BallCloudGradient(this, dt, q);
        }

        public BallCloudGradient GetGradient()
        {
            return new BallCloudGradient(this);
        }

    }

    /// <summary>
    /// The gradient of a matter cloud.
    /// </summary>
    public class BallCloudGradient : IGradient<BallCloud, BallCloudGradient>
    {
        private readonly double[] dm;
        private readonly double[] dv;
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
        public BallCloudGradient(int n)
        {
            this.velocities = new Vector3[n];
            this.accelerations = new Vector3[n];
            this.dm = new double[n];
            this.dv = new double[n];
        }

        public BallCloudGradient(BallCloud c) : this(c.Positions.Length)
        {
            int n = c.Positions.Length;

            // dm and dv are already filled with zeros.
            Array.Copy(c.Velocities, this.velocities, n);

            // Compute graviational accelerations:
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) {
                    if (i == j) continue;

                    var r = c.Positions[j] - c.Positions[i];

                    accelerations[i] += (r / r.Magnitude) * G * c.Masses[j] / (r * r);
                }
        }

        public BallCloudGradient(BallCloud a, double dt, BallCloud b) : this(a.Positions.Length)
        {
            for (int i = 0; i < a.Positions.Length; i++)
            {
                this.velocities[i] = (b.Positions[i] - a.Positions[i]) / dt;
                this.accelerations[i] = (b.Velocities[i] - a.Velocities[i]) / dt;
                this.dm[i] += (b.Masses[i] - a.Masses[i]) / dt;

                var r1 = b.Radii[i];
                var r2 = a.Radii[i];

                this.dv[i] += (4.0 / 3.0 * Math.PI * (r1 * r1 * r1 - r2 * r2 * r2)) / dt;
            }
        }

        /// <summary>
        /// At index i this array contains the velocity of the i-th ball mass.
        /// </summary>
        public Vector3[] Velocities
        {
            get { return velocities; }
        }

        /// <summary>
        /// At index i this array contains the acceleration affecting the i-th ball mass.
        /// </summary>
        public Vector3[] Accelerations
        {
            get { return accelerations; }
        }

        /// <summary>
        /// At index i this array contains the rate at which the i-th ball mass is gaining in mass.
        /// </summary>
        public double[] MassRates
        {
            get { return dm; }
        }      

        /// <summary>
         /// At index i this array contains the rate at which the i-th ball mass is gaining in volume.
         /// </summary>
        public double[] VolumeRates
        {
            get { return dv; }
        }

        public void Add(BallCloud q, double dt)
        {
            q.Add(this, dt);
        }

        public void Add(double f, BallCloudGradient b)
        {
            for (int i = 0; i < velocities.Length; i++)
            {
                accelerations[i] += f * b.accelerations[i];
                velocities[i] += f * b.velocities[i];
                dm[i] += f * b.dm[i];
                dv[i] += f * b.dv[i];
            }
        }

        public BallCloudGradient Copy()
        {
            var n = velocities.Length;
            var copy = new BallCloudGradient(n);
            Array.Copy(this.accelerations, copy.accelerations, n);
            Array.Copy(this.velocities, copy.velocities, n);
            Array.Copy(this.dm, copy.dm, n);
            Array.Copy(this.dv, copy.dv, n);
            return copy;
        }
    }
}
