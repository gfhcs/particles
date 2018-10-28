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

        [Fact()]
        public void TestMoonEarth()
        {
            var path = "/tmp/testMoonEarth.avi";
            var file = new FileStream(path, FileMode.Create);

            int w = 800;
            int h = 600;
            int r = Math.Min(w, h) / 10;
            var fps = 60;
            var visualDuration = 60.0;
            var simulatedDuration = 2 * 30 * 24 * 60 * 60.0;

            var state = new MatterCloud(2);

            var stepSize = 60 * 60.0;
            state.Positions[0] = new Vector3(0, 0, 0);
            state.Positions[1] = new Vector3(362600000, 0, 0);

            state.Masses[0] = 5.97237E24;
            state.Masses[1] = 7.342E22;

            state.Velocities[0] = new Vector3(0, 0, 0);
            state.Velocities[1] = new Vector3(0, 1022, 0);

            var scale = 0.5 * (1.0 / 362600000);

            var dt = (simulatedDuration / visualDuration) / fps;

            var bitmap = new Bitmap(w, h);

            using (var vw = new VideoWriter(file, VideoCodec.H264, w, h, fps))
                for (var sim = new Simulation<MatterCloud, MatterCloudGradient>(state, new RK4<MatterCloud, MatterCloudGradient>(), stepSize); 
                     sim.Time < simulatedDuration; sim.Advance(dt))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Black);

                    foreach (var p in sim.State.Positions)
                    {
                        var x = (int)(scale * p.X);
                        var y = (int)(scale * p.Y);
                        g.FillEllipse(Brushes.White, x - r / 2, y - r / 2, r, r);
                    }
                }

                 vw.Append(bitmap);
            }

            vlc(path);
        }
    }
}
