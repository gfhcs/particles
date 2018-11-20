using System;
using Xunit;
using Particles;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

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

            showVideo(path);
        }

        private static void showVideo(string path)
        {
            var args = "--no-one-instance --no-qt-error-dialogs {0} vlc://quit";
            var vlcInfo = new ProcessStartInfo("vlc", string.Format(args, path));

            using (var vlc = Process.Start(vlcInfo)){
                vlc.WaitForExit();
                Assert.Equal(0, vlc.ExitCode);
            }
        }

        /// <summary>
        /// Runs a simulation and plays back its rendering.
        /// </summary>
        /// <param name="initialState">The initial state of the matter cloud to be simulated.</param>
        /// <param name="integrator">An integrator for matter clouds.</param>
        /// <param name="fileName">The file name for the rendering of the simulation, including the extension.</param>
        /// <param name="w">The width of the rendering frame, in pixels.</param>
        /// <param name="h">The height of the rendering frame, in pixels.</param>
        /// <param name="scale">The factor by which physical dimensions are scaled for rendering.</param>
        /// <param name="fps">The number of frames per second in the rendering.</param>
        /// <param name="stepSize">The step size for the integrator, in seconds.</param>
        /// <param name="visualDuration">The duration of the video to be rendered, in seconds.</param>
        /// <param name="simulatedDuration">The amount of time the simulation should cover, in seconds.</param>
        /// <param name="expectedPerformance">
        /// The expected performance of the test case on the executing machine.
        /// </param>
        private async Task<TestPerformance> TestSimulation(BallCloud initialState,
                                          IIntegrator<BallCloud, BallCloudGradient> integrator,
                                          string fileName,
                                          int w = 800,
                                          int h = 600,
                                          double scale = 0.5 * (1.0 / 149597870700) * 600,
                                          double fps = 25,
                                          double stepSize = 86400,
                                          double visualDuration = 60.0,
                                          double simulatedDuration = 365 * 86400,
                                          TestPerformance expectedPerformance = default(TestPerformance))
        {
            var path = string.Format("/tmp/{0}", fileName);
            var file = new FileStream(path, FileMode.Create);

            var state = initialState;

            var dt = (simulatedDuration / visualDuration) / fps;

            var performance = new TestPerformance();

            var expectedSPS = expectedPerformance.SimulationTime.Rate;
            var expectedRPS = expectedPerformance.RenderingTime.Rate;

            var mutex = new object();

            var stateBuffer = new BlockingCollection<BallCloud>();

            var totalTime = Stopwatch.StartNew();

            var simulation = Task.Run(() =>
            {
                var watch = Stopwatch.StartNew();

                var sim = new Simulation<BallCloud, BallCloudGradient>(state, integrator, stepSize);

                stateBuffer.Add(sim.State);

                int stepCount = 0;
                try
                {
                    while (sim.Time < simulatedDuration)
                    {
                        watch.Restart();
                        sim.Advance(dt);
                        watch.Stop();
                        stepCount++;
                        stateBuffer.Add(sim.State);

                        lock (mutex)
                        {
                            performance = performance.AddSimulationSteps(new TimeFraction(watch.Elapsed.TotalSeconds));
                            if (stepCount >= 10 || !(sim.Time < simulatedDuration))
                            {
                                var sps = performance.SimulationTime.Rate;
                                Assert.True(double.IsNaN(expectedSPS) || sps >= expectedSPS, string.Format("Can only simulate {0} steps per second, but expected at least {1}!", sps, expectedSPS));
                            }
                        }
                    }
                }
                finally
                {
                    stateBuffer.CompleteAdding();
                }
            });

            var rendering = Task.Run(async () =>
            {
                var watch = new Stopwatch();
                var stepCount = 0;
                using (var renderer = new BallCloudRenderer(w, h, scale))
                using (var vw = new VideoWriter(file, VideoCodec.H264, w, h, fps))
                {
                    try
                    {
                        while (!stateBuffer.IsCompleted)
                        {
                            var c = stateBuffer.Take();
                            watch.Restart();
                            vw.Append(await renderer.Render(c));
                            watch.Stop();
                            stepCount++;

                            lock (mutex)
                            {
                                performance = performance.AddRenderingSteps(new TimeFraction(watch.Elapsed.TotalSeconds));

                                if (stepCount >= 10 || stateBuffer.IsCompleted)
                                {
                                    var rps = performance.RenderingTime.Rate;
                                    Assert.True(double.IsNaN(expectedRPS) || rps >= expectedRPS, string.Format("Can only render {0} frames per second, but expected at least {1}!", rps, expectedRPS));
                                }
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // This happens if stateBuffer.CompleteAdding is called between our check to IsCompleted and our call to Take.
                    }
                }
            });

            await simulation;
            await rendering;

            performance = performance.AddTotalTime(totalTime.Elapsed.TotalSeconds);

            Assert.True(expectedPerformance.TotalTime == 0 || performance.TotalTime <= expectedPerformance.TotalTime, string.Format("The test case used {0} seconds, but was expected to take at most {1} seconds.", performance.TotalTime, expectedPerformance.TotalTime));

            showVideo(path);
            return performance;
        }

        [Fact()]
        public async Task TestEarthMoon()
        {
            var fileName = "testEarthMoon.avi";

            int w = 1920;
            int h = 1080;
            var scale = 0.5 * (1.0 / 362600000) * Math.Min(w, h);

            var fps = 60;
            var visualDuration = 60.0;
            var simulatedDuration = 2 * 30 * 24 * 60 * 60.0;

            var state = new BallCloud(2);

            var stepSize = 60 * 60.0;
            state.Positions[0] = new Vector3(0, 0, 0);
            state.Positions[1] = new Vector3(362600000, 0, 0);

            state.Masses[0] = 5.97237E24;
            state.Masses[1] = 7.342E22;

            state.Velocities[0] = new Vector3(0, -11, 0);
            state.Velocities[1] = new Vector3(0, 1022, 0);

            state.Radii[0] = 6371000.0;
            state.Radii[1] = 1737100.0;

            await TestSimulation(state, new RK4<BallCloud, BallCloudGradient>(), fileName, w, h, scale, fps, stepSize, visualDuration, simulatedDuration);
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

            showImage(bitmap);
        }

        private void showImage(Image img)
        {
            var path = "/tmp/image.png";

            img.Save(path);

            var gwenview = new ProcessStartInfo("gwenview", path);

            gwenview.UseShellExecute = false;

            using (var p = Process.Start(gwenview))
                p.WaitForExit();
        }

        [Fact()]
        void TestSample2()
        {
            var n = 1000000;
            var r = 128;
            var path = "/tmp/ballsampletest.avi";
            var file = new FileStream(path, FileMode.Create);

            var bitmap = new Bitmap(2 * r, 2 * r);

            var samples = new Vector3[n];
            for (int i = 0; i < n; i++)
                samples[i] = rndv.NextVector(r);
            Array.Sort(samples);

            int k = 0;
            using (var vw = new VideoWriter(file, VideoCodec.H264, 2 * r, 2 * r, 30))
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

            showVideo(path);
        }

        /// <summary>
        /// Runs and renders a simulation of a random matter cloud.
        /// </summary>
        /// <param name="n">The number of particles in the cloud.</param>
        /// <param name="size">The initial size of the matter cloud.</param>
        /// <param name="mass">The total mass of the cloud.</param>
        /// <param name="internalEnergy">The total kinetic energy of all the particles in the cloud.</param>
        /// <param name="radius">The size of the particles</param>
        /// <param name="expectedPerformance">
        /// The expected performance of the test case on the executing machine.
        /// </param>
        Task<TestPerformance> TestRandomCloud(int n, double size, double mass, double radius, double internalEnergy, double stepSize, double simulatedDuration, TestPerformance expectedPerformance = default(TestPerformance))
        {
            int w = 1920;
            int h = 1080;
            double scale = 0.25 * h / size;
            double fps = 60;
            double visualDuration = (simulatedDuration / stepSize) / fps;

            var initial = new BallCloud(n);

            var m = mass / n;

            var R = 0.0;
            var E = 0.0;

            for (int i = 0; i < n; i++){
                
                var p = rndv.NextVector(size);
                var v = rndv.NextVector(1);

                initial.Masses[i] = m;
                initial.Positions[i] = p;
                initial.Velocities[i] = v;
                initial.Radii[i] = radius;

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

            return TestSimulation(initial, new RK4<BallCloud, BallCloudGradient>(), string.Format("cloud{0}.avi", n), w, h, scale, fps, stepSize, visualDuration, simulatedDuration, expectedPerformance);
        }

        /// <summary>
        /// A minimal random cloud, of two particles.
        /// </summary>
        [Fact()]
        public async Task TestBinaryOscillation()
        {
            var fileName = "testBinaryOscillation.avi";

            int w = 1600;
            int h = 900;
            var scale = 0.475 * (1.0 / 362600000) * w;

            var fps = 60;
            var visualDuration = 60.0;
            var simulatedDuration = 2 * 30 * 24 * 60 * 60.0;

            var state = new BallCloud(2);

            var stepSize =  0.1;
            state.Positions[0] = new Vector3(0, 0, 0);
            state.Positions[1] = new Vector3(362600000, 0, 0);

            state.Masses[0] = 5.97237E24;
            state.Masses[1] = 7.342E22;

            state.Radii[0] = 10 * 6371000.0;
            state.Radii[1] = 10 * 1737100.0;

            await TestSimulation(state, new RK4<BallCloud, BallCloudGradient>(), fileName, w, h, scale, fps, stepSize, visualDuration, simulatedDuration);
        }


        /// <summary>
        /// A minimal random cloud, of 100 particles.
        /// </summary>
        [Fact()]
        public void TestCloud1()
        {
            var n = 100;
            var m = 7.342E22; // Mass of the moon
            var r = 15 * 1737100.0; // Multiple of the radius of the moon
            var v = 0.5 * 1022.0; // Multiple of the velocity of the moon
            var s = 2 * 385001000.0; // multiple distance between Moon and Earth
            var d = 1 * 3600.0; // 1 hour
            var D = 10 * 12 * 30 * 86400.0; // 10 years
            TestRandomCloud(n, s, n * m, r, n * 0.5 * m * v * v, d, D);
        }

        /// <summary>
        /// Tests retrieval of machine parameters.
        /// </summary>
        [Fact()]
        public void TestMachineInfo()
        {
            MachineInfo m = MachineInfo.GetRunning();
            Assert.Equal(m, m);
        }

        /// <summary>
        /// Enumerates all the benchmarks for the current machine.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{Tuple{int, double}}"/> of pairs (n, f),
        /// where n is the number of particles and f is the rate at which
        /// the machine should be able to render frames for a simulation containing
        /// n particles.
        /// </returns>
        public static IEnumerable<object[]> EnumerateBenchmarks()
        {
            // For each known machine, we have a set of benchmark tuples (n, f),
            // where n is a number of particles and f is the number of frames per second
            // at which this machine should be able to render the simulation
            var benchmarks = new Dictionary<MachineInfo, List<(int, double, double, double)>>();

            var laptop = new MachineInfo("gereon-laptop.gereon", 3000000, 4, (long)8049484 * 1024, OperatingSystem.Linux);
            benchmarks[laptop] = new List<(int, double, double, double)>();
            benchmarks[laptop].Add((1, 3450, 50, 1.0));
            benchmarks[laptop].Add((10, 2450, 50, 1.0));
            benchmarks[laptop].Add((100, 105, 47, 1.1));
            benchmarks[laptop].Add((250, 20, 45, 1.5));
            benchmarks[laptop].Add((500, 6.5, 45, 4.25));
            benchmarks[laptop].Add((750, 3, 34, 8.5));
            benchmarks[laptop].Add((1024, 1.5, 27.5, 15));

            var desktopMachine = new MachineInfo("gereon-desktop", 3800000, 12, (long)67465666560, OperatingSystem.Linux);
            benchmarks[desktopMachine] = new List<(int, double, double, double)>();
            benchmarks[desktopMachine].Add((1, 3500, 60, 0.75));
            benchmarks[desktopMachine].Add((10, 3000, 60, 0.75));
            benchmarks[desktopMachine].Add((100, 150, 60, 0.75));
            benchmarks[desktopMachine].Add((250, 25, 55, 1.1));
            benchmarks[desktopMachine].Add((500, 8, 55, 3.5));
            benchmarks[desktopMachine].Add((750, 3.5, 40, 7));
            benchmarks[desktopMachine].Add((1024, 1.75, 29.5, 12));

            /*
            var officeMachine = new MachineInfo("baraddur.cs.uni-saarland.de", 4000000, 8, (long)33604718592, OperatingSystem.Linux);
            benchmarks[officeMachine] = new List<Tuple<int, double>>();
            benchmarks[officeMachine].Add(Tuple.Create(1, 11.25));
            benchmarks[officeMachine].Add(Tuple.Create(10, 11.0));
            benchmarks[officeMachine].Add(Tuple.Create(100, 9.25));
            benchmarks[officeMachine].Add(Tuple.Create(500, 2.0));
            benchmarks[officeMachine].Add(Tuple.Create(750, 1.0));
            */

            var rm = MachineInfo.GetRunning();

            foreach (var bm in benchmarks[rm])
                yield return new object[] { bm.Item1, bm.Item2, bm.Item3, bm.Item4 };
        }

        /// <summary>
        /// This test case picks a number of particles depending on the specs of the current machine
        /// and asserts that the machine is able to render at least 10 frames per second for a random
        /// cloud with this many particles.
        /// </summary>
        /// <param name="n">The number of particles to intialize the simulation with.</param>
        /// <param name="sps">The expected rate at which simulation steps can be computed on the executing machine.</param>
        /// <param name="rps">The expected rate at which frames can be rendered on the executing machine.</param>
        /// <param name="T">The expected total time the test should take on the executing machine.</param>
        [Theory()]
        [MemberData(nameof(EnumerateBenchmarks))]
        public async Task TestPerformance(int n, double sps, double rps, double T)
        {
            var m = 7.342E22; // Mass of the moon
            var r = 15 * 1737100.0; // Multiple of the radius of the moon
            var v = 0.5 * 1022.0; // Multiple of the velocity of the moon
            var s = 2 * 385001000.0; // multiple distance between Moon and Earth
            var d = 1 * 3600.0; // 1 hour
            var D = 1 * 86400.0; // 1 day

            var sRate = TimeFraction.FromRate(sps);
            var rRate = TimeFraction.FromRate(rps);

            var expectedPerformance = new TestPerformance(sRate, rRate, T);

            await TestRandomCloud(n, s, n * m, r, n * 0.5 * m * v * v, d, D, expectedPerformance);
        }
    }
}
