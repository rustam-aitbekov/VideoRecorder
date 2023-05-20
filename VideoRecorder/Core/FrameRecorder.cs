using OpenCvSharp;
using VideoRecorder.Exceptions;
using Size = OpenCvSharp.Size;

namespace VideoRecorder.Core
{
    internal class FrameRecorder : IDisposable
    {
        private bool _disposed;

        public delegate void FrameRecordingStartedEventHandler();
        public event FrameRecordingStartedEventHandler? FrameRecordingStarted;

        private readonly VideoCaptureAPIs _videoCaptureApi = VideoCaptureAPIs.DSHOW;
        private readonly ManualResetEventSlim _threadStopEvent = new(false);
        private readonly VideoCapture _videoCapture;
        private VideoWriter _videoWriter;

        private Mat _capturedFrame = new();
        private Thread _captureThread;
        private Thread _writerThread;
        private bool invoked = false;

        private bool IsVideoCaptureValid => _videoCapture is not null && _videoCapture.IsOpened();

        public FrameRecorder(int deviceIndex, int frameWidth, int frameHeight, double fps)
        {
            _videoCapture = VideoCapture.FromCamera(deviceIndex, _videoCaptureApi);
            _videoCapture.Open(deviceIndex, _videoCaptureApi);

            _videoCapture.FrameWidth = frameWidth;
            _videoCapture.FrameHeight = frameHeight;
            _videoCapture.Fps = fps;
        }

        ~FrameRecorder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopRecording();

                    _videoCapture?.Release();
                    _videoCapture?.Dispose();
                }
                _disposed = true;
            }
        }

        public void StartRecording(string path)
        {
            if (_writerThread is not null)
                return;

            if (!IsVideoCaptureValid)
                ThrowHelper.ThrowVideoCaptureNotReadyException();

            _videoWriter = new VideoWriter(path, FourCC.XVID, _videoCapture.Fps, new Size(_videoCapture.FrameWidth, _videoCapture.FrameHeight));

            _threadStopEvent.Reset();

            _captureThread = new Thread(CaptureFrameLoop);
            _captureThread.Start();

            _writerThread = new Thread(AddCameraFrameToRecordingThread);
            _writerThread.Start();

            
        }

        public void StopRecording()
        {
            _threadStopEvent.Set();

            _writerThread?.Join();
            _writerThread = null;

            _captureThread?.Join();
            _captureThread = null;

            _threadStopEvent.Reset();

            _videoWriter?.Release();
            _videoWriter?.Dispose();
            _videoWriter = null;
        }

        private void CaptureFrameLoop()
        {
            while (!_threadStopEvent.Wait(0))
            {
                _videoCapture.Read(_capturedFrame);
            }
        }

        private void AddCameraFrameToRecordingThread()
        {
            var waitTimeBetweenFrames = 1_000 / _videoCapture.Fps;
            var lastWrite = DateTime.Now;

            while (!_threadStopEvent.Wait(0))
            {
                if (DateTime.Now.Subtract(lastWrite).TotalMilliseconds >= waitTimeBetweenFrames)
                {
                    lastWrite = DateTime.Now;
                    _videoWriter.Write(_capturedFrame);

                    if (!invoked) 
                    { 
                        FrameRecordingStarted?.Invoke(); 
                        invoked = true;
                    }
                }
            }
        }

        public Mat GetFrame()
        {
            return _capturedFrame;
        }
    }
}