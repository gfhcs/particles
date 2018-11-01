using System;
using Xunit;
using Particles;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Linq;

namespace Tests
{
    public class Test
    {
        private const double au = 149597870700.0;

        [Fact()]
        public void TestVideoWriter()
        {
            var path = "/tmp/videoWriterTest.avi";

            var file = new FileStream(path, FileMode.Create);

            int w = 800;
            int h = 600;
            int fps = 10;

            using (var vw = new VideoWriter(file, VideoCodec.H264, w, h, fps))
            {
                for (int s = 0; s < 3; s++)
                    for (int f = 0; f < fps; f++)
                    {
                        var img = new Bitmap(w, h);
                        using (var g = Graphics.FromImage(img))
                            g.DrawString(f.ToString(), new Font(FontFamily.GenericMonospace, 60), Brushes.Red, new PointF(0, 0));

                        vw.Append(img);
                    }
            }

            vlc(path);
        }

        private static void vlc(string path)
        {
            var args = "--no-one-instance --no-qt-error-dialogs {0} vlc://quit";
            var vlcInfo = new ProcessStartInfo("vlc", string.Format(args, path));

            var vlc = Process.Start(vlcInfo);
            vlc.WaitForExit();

            Assert.Equal(0, vlc.ExitCode);
        }

        /// <summary>
        /// Runs a simulation and plays back its rendering.
        /// </summary>
        /// <param name="initialState">The initial state of the matter cloud to be simulated.</param>
        /// <param name="integrator">An integrator for matter clouds.</param>
        /// <param name="fileName">The file name for the rendering of the simulation, including the extension.</param>
        /// <param name="radius">The size with particles are rendered, in pixels.</param>
        /// <param name="w">The width of the rendering frame, in pixels.</param>
        /// <param name="h">The height of the rendering frame, in pixels.</param>
        /// <param name="scale">The factor by which physical dimensions are scaled for rendering.</param>
        /// <param name="fps">The number of frames per second in the rendering.</param>
        /// <param name="stepSize">The step size for the integrator, in seconds.</param>
        /// <param name="visualDuration">The duration of the video to be rendered, in seconds.</param>
        /// <param name="simulatedDuration">The amount of time the simulation should cover, in seconds.</param>
        private void TestSimulation(MatterCloud initialState,
                                          IIntegrator<MatterCloud, MatterCloudGradient> integrator,
                                          string fileName,
                                          int radius = 1,
                                          int w=800,
                                          int h=600,
                                          double scale = 0.5 * (1.0 / 149597870700) * 600,
                                          double fps=25,
                                          double stepSize = 86400,
                                          double visualDuration=60.0, 
                                          double simulatedDuration=365*86400)
        {
            var path = string.Format("/tmp/{0}", fileName);
            var file = new FileStream(path, FileMode.Create);

            var state = initialState;

            var dt = (simulatedDuration / visualDuration) / fps;

            var bitmap = new Bitmap(w, h);

            using (var vw = new VideoWriter(file, VideoCodec.H264, w, h, fps))
                for (var sim = new Simulation<MatterCloud, MatterCloudGradient>(state, integrator, stepSize);
                     sim.Time < simulatedDuration; sim.Advance(dt))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.Clear(Color.Black);

                        foreach (var p in sim.State.Positions)
                        {
                            var x = w / 2 + (int)(scale * p.X);
                            var y = h / 2 - (int)(scale * p.Y);
                            g.FillEllipse(Brushes.White, x - radius / 2, y - radius / 2, radius, radius);
                        }
                    }

                    vw.Append(bitmap);
                }

            vlc(path);
        }

        [Fact()]
        public void TestMoonEarth()
        {
            var fileName = "testMoonEarth.avi";

            int radius = 50;

            int w = 800;
            int h = 600;
            var scale = 0.5 * (1.0 / 362600000) * Math.Min(w, h);

            var fps = 15;
            var visualDuration = 60.0;
            var simulatedDuration = 2 * 30 * 24 * 60 * 60.0;

            var state = new MatterCloud(2);

            var stepSize = 24 * 60 * 60.0;
            state.Positions[0] = new Vector3(0, 0, 0);
            state.Positions[1] = new Vector3(362600000, 0, 0);

            state.Masses[0] = 5.97237E24;
            state.Masses[1] = 7.342E22;

            state.Velocities[0] = new Vector3(0, 0, 0);
            state.Velocities[1] = new Vector3(0, 1022, 0);

            TestSimulation(state, new RK4<MatterCloud, MatterCloudGradient>(), fileName, radius, w, h, scale, fps, stepSize, visualDuration, simulatedDuration);
        }

        RandomVector rndv = new RandomVector(new Random());

        [Fact()]
        void TestSample1()
        {
            var r = 512;

            var bitmap = new Bitmap(2 * r, 2 * r);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.Clear(Color.Black);

                for (int i = 0; i < 100000; i++)
                {
                    var s = new Vector3(r, r, r) + rndv.NextVector(r);
                    g.FillEllipse(Brushes.White, (int)s.X, (int)s.Y, 1, 1);
                }
            }

            var path = "/tmp/disksampletest.png";

            bitmap.Save(path);

            var gwenview = new ProcessStartInfo("gwenview", path);

            gwenview.UseShellExecute = false;

            Process.Start(gwenview).WaitForExit();
        }

        [Fact()]
        void TestSample2()
        {
            var n = 1000000;
            var r = 128;
            var path = "/tmp/disksampletest.avi";
            var file = new FileStream(path, FileMode.Create);

            var bitmap = new Bitmap(2 * r, 2 * r);

            var samples = new Vector3[n];
            for (int i = 0; i < n; i++)
                samples[i] = rndv.NextVector(r);
            Array.Sort(samples);

            int k = 0;
            using (var vw = new VideoWriter(file, VideoCodec.H264, 2 * r, 2 * r, 1))
                for (int i = -r; i < r; i++) {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.Clear(Color.Black);

                        for (; k < n && samples[k].X <= i; k++)
                        {
                            var s = samples[k];
                            var x = r + (int)(s.Y);
                            var y = r - (int)(s.Z);
                            g.FillEllipse(Brushes.White, x, y, 1, 1);
                        }
                    }

                    vw.Append(bitmap);
                }

            vlc(path);
        }

        /// <summary>
        /// Runs and renders a simulation of a random matter cloud.
        /// </summary>
        /// <param name="n">The number of particles in the cloud.</param>
        /// <param name="size">The initial size of the matter cloud.</param>
        /// <param name="mass">The total mass of the cloud.</param>
        /// <param name="internalEnergy">The total kinetic energy of all the particles in the cloud.</param>
        void TestRandomCloud(int n, double size, double mass, double internalEnergy, double stepSize, double simulatedDuration)
        {
            int w = 1920;
            int h = 1080;
            double scale = 0.5 * h / size;
            double fps = 25;
            double visualDuration = (simulatedDuration / stepSize) / fps;

            var initial = new MatterCloud(n);

            var m = mass / n;

            var R = 0.0;
            var E = 0.0;

            for (int i = 0; i < n; i++){
                
                var p = rndv.NextVector(size);
                var v = rndv.NextVector(1);

                initial.Masses[i] = m;
                initial.Positions[i] = p;
                initial.Velocities[i] = v;

                R = Math.Max(R, p.Magnitude);
                E += 0.5 * m * v * v;
            }

            var rf = size / R;
            var ef = Math.Sqrt(internalEnergy / E);

            for (int i = 0; i < n; i++)
            {
                initial.Positions[i] *= rf;
                initial.Velocities[i] *= ef;
            }

            TestSimulation(initial, new RK4<MatterCloud, MatterCloudGradient>(), string.Format("cloud{0}.avi", n), 5, w, h, scale, fps, stepSize, visualDuration, simulatedDuration);
        }

        /// <summary>
        /// A minimal random cloud, of two particles.
        /// </summary>
        [Fact()]
        public void TestBinaryOscillation()
        {
            var fileName = "testBinaryOscillation.avi";

            int radius = 50;

            int w = 800;
            int h = 600;
            var scale = 0.5 * (1.0 / 362600000) * Math.Min(w, h);

            var fps = 15;
            var visualDuration = 60.0;
            var simulatedDuration = 2 * 30 * 24 * 60 * 60.0;

            var state = new MatterCloud(2);

            var stepSize = 24 * 60 * 60.0;
            state.Positions[0] = new Vector3(0, 0, 0);
            state.Positions[1] = new Vector3(362600000, 0, 0);

            state.Masses[0] = 5.97237E24;
            state.Masses[1] = 7.342E22;

            TestSimulation(state, new RK4<MatterCloud, MatterCloudGradient>(), fileName, radius, w, h, scale, fps, stepSize, visualDuration, simulatedDuration);
        }


        /// <summary>
        /// A minimal random cloud, of two particles.
        /// </summary>
        [Fact()]
        public void TestCloud1()
        {
            var n = 10;
            var m = 7.342E22; // Mass of the moon
            var v = 0.1 * 1022.0; // Multiple of the velocity of the moon
            var s = 2 * 385001000.0; // multiple distance between Moon and Earth
            var d = 86400; // 1 day
            var D = 120 * 30 * 86400; // 10 years
            TestRandomCloud(n, s, n * m, n * 0.5 * m * v * v, d, D);
        }
    }
}
