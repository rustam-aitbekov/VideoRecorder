using System.Diagnostics;
using OpenCvSharp;
using VideoRecorder.Exceptions;

namespace VideoRecorder.Core
{
    /// <summary>
    /// Captures camera frames on one thread and writes them at a constant frame rate on another.
    /// </summary>
    internal sealed class FrameRecorder : IDisposable
    {
        /// <summary>Raised on the writer thread once the first frame has been written.</summary>
        public event Action? FrameRecordingStarted;

        private readonly VideoCapture _videoCapture;
        private readonly double _fps;
        private readonly ManualResetEventSlim _threadStopEvent = new(false);

        // Shared between the capture thread (writer), the writer thread and GetFrame (readers).
        private readonly object _frameLock = new();
        private readonly Mat _latestFrame = new();
        private bool _hasFrame;

        private Thread? _captureThread;
        private Thread? _writerThread;
        private Exception? _writerError;
        private bool _disposed;

        public FrameRecorder(int deviceIndex, int frameWidth, int frameHeight, double fps)
        {
            if (fps <= 0)
                throw new ArgumentOutOfRangeException(nameof(fps), fps, "Frame rate must be positive.");

            _fps = fps;
            _videoCapture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);

            _videoCapture.FrameWidth = frameWidth;
            _videoCapture.FrameHeight = frameHeight;
            _videoCapture.Fps = fps;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            StopThreads();

            _videoCapture.Release();
            _videoCapture.Dispose();
            _latestFrame.Dispose();
            _threadStopEvent.Dispose();
        }

        public void StartRecording(string path)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_writerThread is not null)
                throw new InvalidOperationException("Frame recording has already been started.");

            if (!_videoCapture.IsOpened())
                ThrowHelper.ThrowVideoCaptureNotReadyException();

            _threadStopEvent.Reset();
            _writerError = null;

            _captureThread = new Thread(CaptureFrameLoop) { IsBackground = true, Name = "VideoRecorder capture" };
            _captureThread.Start();

            _writerThread = new Thread(() => WriteFrameLoop(path)) { IsBackground = true, Name = "VideoRecorder writer" };
            _writerThread.Start();
        }

        /// <summary>
        /// Stops both threads and closes the video file. Rethrows any error that happened on the writer thread.
        /// </summary>
        public void StopRecording()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            StopThreads();

            if (_writerError is { } error)
            {
                _writerError = null;
                throw new InvalidOperationException("Writing the video frames failed.", error);
            }
        }

        private void StopThreads()
        {
            _threadStopEvent.Set();

            _writerThread?.Join();
            _writerThread = null;

            _captureThread?.Join();
            _captureThread = null;
        }

        private void CaptureFrameLoop()
        {
            using var buffer = new Mat();

            while (!_threadStopEvent.IsSet)
            {
                // Read blocks until the camera delivers the next frame.
                if (!_videoCapture.Read(buffer) || buffer.Empty())
                {
                    // Camera not ready or unplugged: back off instead of spinning.
                    _threadStopEvent.Wait(10);
                    continue;
                }

                lock (_frameLock)
                {
                    buffer.CopyTo(_latestFrame);
                    _hasFrame = true;
                }
            }
        }

        private bool TryCopyLatestFrame(Mat destination)
        {
            lock (_frameLock)
            {
                if (!_hasFrame)
                    return false;

                _latestFrame.CopyTo(destination);
                return true;
            }
        }

        private void WriteFrameLoop(string path)
        {
            try
            {
                using var frame = new Mat();

                // Wait for the camera's first frame; its size is what the camera actually delivers.
                while (!TryCopyLatestFrame(frame))
                {
                    if (_threadStopEvent.Wait(5))
                        return;
                }

                var frameSize = frame.Size();
                using var videoWriter = new VideoWriter(path, FourCC.MJPG, _fps, frameSize);
                if (!videoWriter.IsOpened())
                    throw new InvalidOperationException($"Could not open a video writer for '{path}'.");

                using var resized = new Mat();
                var clock = Stopwatch.StartNew();
                long framesWritten = 0;

                FrameRecordingStarted?.Invoke();

                while (true)
                {
                    // Write as many frames as the elapsed time requires: if we fell behind, the latest frame is
                    // repeated, so the video's duration matches real time and stays in sync with the audio.
                    long framesDue = (long)(clock.Elapsed.TotalSeconds * _fps) + 1;
                    while (framesWritten < framesDue)
                    {
                        TryCopyLatestFrame(frame);

                        if (frame.Size() != frameSize)
                        {
                            Cv2.Resize(frame, resized, frameSize);
                            videoWriter.Write(resized);
                        }
                        else
                        {
                            videoWriter.Write(frame);
                        }

                        framesWritten++;
                    }

                    var untilNextFrame = TimeSpan.FromSeconds(framesWritten / _fps) - clock.Elapsed;
                    if (_threadStopEvent.Wait(untilNextFrame > TimeSpan.Zero ? untilNextFrame : TimeSpan.Zero))
                        return;
                }
            }
            catch (Exception ex)
            {
                _writerError = ex;
            }
        }

        /// <summary>
        /// Returns a copy of the most recent camera frame (empty if none has been captured yet).
        /// The caller owns the returned <see cref="Mat"/> and should dispose it.
        /// </summary>
        public Mat GetFrame()
        {
            var copy = new Mat();
            TryCopyLatestFrame(copy);
            return copy;
        }
    }
}
