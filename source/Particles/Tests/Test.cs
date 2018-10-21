using System;
using Xunit;
using Particles;
using System.IO;
using System.Drawing;
namespace Tests
{
    public class Test
    {
        [Fact()]
        public void TestVideoWriter()
        {
            var file = new FileStream("/tmp/videoWriterTest.mp4", FileMode.Create);

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
        }
    }
}
