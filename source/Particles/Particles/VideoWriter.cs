using System;
using System.Drawing;
using System.IO;

namespace Particles
{
    /// <summary>
    /// A mode of encoding a video stream internally
    /// </summary>
    public enum VideoCodec
    {
        H264
    }

    /// <summary>
    /// Represents a video file on the hard drive, to which frames can be appended.
    /// </summary>
    public class VideoWriter : IDisposable
    {
        private Stream stream;
        private readonly VideoCodec codec;
        private readonly int width, height;
        private readonly double fps;

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

            // TODO: Launch FFMPEG and make it write to 'stream'!

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
        /// Appends a frame to the video
        /// </summary>
        /// <exception cref="ArgumentException">If the given image does not have the same width and height as the video stream.</exception>
        /// <returns>The append.</returns>
        /// <param name="frame">Frame.</param>
        public void Append(Image frame)
        {
            //TODO: Pass frame to FFMPEG!
        }

        /// <summary>
        /// Closes the video writer and the underlying stream.
        /// </summary>
        public void Close()
        {
            if (stream == null)
                throw new InvalidOperationException("This VideoWriter object has already been closed!");

            // TODO: Close FFMPEG communcation!

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
