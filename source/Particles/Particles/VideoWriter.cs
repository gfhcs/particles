using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;

namespace Particles
{
    /// <summary>
    /// A mode of encoding a video stream internally
    /// </summary>
    public enum VideoCodec
    {
        H264,
        MJPEG
    }

    /// <summary>
    /// Represents a video file on the hard drive, to which frames can be appended.
    /// </summary>
    /// <remarks>
    /// The implementation of this class is based on FFMPEG.
    /// When FFMPEG rejects input data, exceptions thrown by this class might not
    /// always be sufficiently specific. Causes observed for such exceptions include:
    /// - Target resolution being too low.
    /// </remarks>
    public class VideoWriter : IDisposable
    {
        private static string codec2string(VideoCodec codec)
        {
            switch(codec)
            {
                case VideoCodec.H264:
                    return "libx264";
                case VideoCodec.MJPEG:
                    return "mjpeg";
                default:
                    throw new NotImplementedException(string.Format("The codec {0} has not been accounted for in the implementation of the VideoWriter class!", codec));
            }
        }

        private Process ffmpeg;
        private Stream stream;
        private readonly VideoCodec codec;
        private readonly int width, height;
        private readonly double fps;
        private Exception ffmpegCrash = null;

        /// <summary>
        /// Initializes a new <see cref="T:Particles.VideoWriter"/>.
        /// </summary>
        /// <param name="stream">The stream into which the video is to be written</param>
        /// <param name="codec">The codec to be used for encoding the video.</param>
        /// <param name="width">The frame width, in pixels</param>
        /// <param name="height">The frame height, in pixels</param>
        /// <param name="fps">The number of frames per video-second.</param>
        public VideoWriter(Stream stream, VideoCodec codec, int width, int height, double fps)
        {
            this.stream = stream;
            this.codec = codec;
            this.width = width;
            this.height = height;
            this.fps = fps;

            const string args = "-loglevel error -y -f image2pipe -framerate {1} -i - -c:v {0} -f avi pipe:1";
            var ffmpegInfo = new ProcessStartInfo("ffmpeg", string.Format(args, codec2string(codec), fps));

            ffmpegInfo.UseShellExecute = false;
            ffmpegInfo.RedirectStandardInput = true;
            ffmpegInfo.RedirectStandardOutput = true;
            ffmpegInfo.RedirectStandardError = true;

            this.ffmpeg = Process.Start(ffmpegInfo);

            ffmpeg.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null && e.Data.Trim().Length > 0)
                {
                    ffmpeg.CancelErrorRead();
                    ffmpegCrash = new IOException(string.Format("FFMPEG reported an error: {0}", e.Data));
                }
            };
            ffmpeg.BeginErrorReadLine();

            ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);
        }

        #region "Properties"

        /// <summary>
        /// The stream into which the video is to be written
        /// </summary>
        /// <value>The base stream.</value>
        public Stream BaseStream
        {
            get {
                return stream;
            }
        }

        /// <summary>
        /// The codec to be used for encoding the video.
        /// </summary>
        public VideoCodec Codec
        {
            get{
                return codec;
            }
        }

        /// <summary>
        /// The dimensions of the frames in the video.
        /// </summary>
        /// <value>The resolution.</value>
        public Size Resolution
        {
            get { return new Size(width, height); }
        }

        /// <summary>
        /// The number of frames per video second.
        /// </summary>
        public double FramesPerSecond
        {
            get{
                return fps;
            }
        }

        #endregion

        /// <summary>
        /// Throws an exception containing information about why FFMPEG crashed.
        /// </summary>
        private void throwCrash()
        {
            if (ffmpegCrash != null)
                throw ffmpegCrash;
        }

        /// <summary>
        /// Appends a frame to the video
        /// </summary>
        /// <exception cref="ArgumentException">If the given image does not have the same width and height as the video stream.</exception>
        /// <returns>The append.</returns>
        /// <param name="frame">Frame.</param>
        public void Append(Image frame)
        {
            throwCrash();
            if (frame.Size != this.Resolution)
                throw new ArgumentException(string.Format("Cannot add a frame of resolution {0}x{1} to a video with resolution {2}x{3} !", frame.Width, frame.Height, width, height));

            frame.Save(ffmpeg.StandardInput.BaseStream, ImageFormat.Jpeg);
        }

        /// <summary>
        /// Closes the video writer and the underlying stream.
        /// </summary>
        public void Close()
        {
            throwCrash();
            if (stream == null)
                throw new InvalidOperationException("This VideoWriter object has already been closed!");

            ffmpeg.StandardInput.BaseStream.Close();
            ffmpeg.WaitForExit();
            stream.Close();
            stream = null;
        }

        public void Dispose()
        {
            if (stream != null)
                Close();
        }
    }
}
