using System;
using Xunit;
using Particles;
using System.IO;
using System.Drawing;
using System.Diagnostics;

namespace Tests
{
    public class Test
    {
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
    }
}
