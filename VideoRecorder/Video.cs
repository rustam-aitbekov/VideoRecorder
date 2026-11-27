using OpenCvSharp;
using VideoRecorder.Core;

namespace VideoRecorder
{
    /// <summary>
    /// Records a camera together with the microphone (and optionally the speakers) into an MP4 file.
    /// </summary>
    public class Video : IDisposable
    {
        private readonly FFmpegRunner _ffmpeg;
        private readonly Camera _camera = new();

        private FrameRecorder? _frameRecorder;
        private AudioRecorder? _audioRecorder;
        private Exception? _audioStartError;

        private string? _frameFilePath;
        private string? _audioFilePath;
        private string? _outputFilePath;
        private int _microphoneIndex;
        private bool _recordSpeaker;
        private bool _disposed;

        /// <summary>Uses the ffmpeg executable found on PATH.</summary>
        public Video() : this("ffmpeg")
        {
        }

        /// <param name="ffmpegPath">
        /// The folder that contains ffmpeg.exe, the full path to ffmpeg.exe, or "ffmpeg" to use the one on PATH.
        /// The ffmpeg build must include libx264 (for example the "full" or "gpl" builds).
        /// </param>
        public Video(string ffmpegPath)
        {
            _ffmpeg = new FFmpegRunner(ffmpegPath);
        }

        /// <summary>True between <see cref="StartRecording"/> and <see cref="StopRecordingAsync"/>.</summary>
        public bool IsRecording => _frameRecorder is not null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Abandons a recording that is still running.
                _frameRecorder?.Dispose();
                _frameRecorder = null;
                _audioRecorder?.Dispose();
                _audioRecorder = null;
                DeleteTempFiles();
                _camera.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Starts recording. Audio starts as soon as the first camera frame has been written, so both stay in sync.
        /// </summary>
        /// <param name="cameraIndex">The <see cref="CameraDevice.Id"/> of the camera.</param>
        /// <param name="resolution">Requested camera resolution; the camera may pick the closest one it supports.</param>
        /// <param name="fps">Frame rate of the output video.</param>
        /// <param name="microphoneIndex">The <see cref="InputAudioDevice.Id"/> of the microphone.</param>
        /// <param name="recordSpeaker">Also record what the default playback device is playing.</param>
        /// <param name="filePath">Output .mp4 file.</param>
        public void StartRecording(int cameraIndex, Resolution resolution, int fps, int microphoneIndex, bool recordSpeaker, string filePath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(resolution);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);

            if (IsRecording)
                throw new InvalidOperationException("A recording is already in progress. Stop it first.");

            string id = Guid.NewGuid().ToString("N");
            _frameFilePath = Path.Combine(Path.GetTempPath(), $"videorecorder_frames_{id}.avi");
            _audioFilePath = Path.Combine(Path.GetTempPath(), $"videorecorder_audio_{id}.wav");
            _outputFilePath = filePath;
            _microphoneIndex = microphoneIndex;
            _recordSpeaker = recordSpeaker;
            _audioStartError = null;

            var frameRecorder = new FrameRecorder(cameraIndex, resolution.Width, resolution.Height, fps);
            try
            {
                frameRecorder.FrameRecordingStarted += FrameRecorder_FrameRecordingStarted;
                frameRecorder.StartRecording(_frameFilePath);
            }
            catch
            {
                frameRecorder.Dispose();
                throw;
            }

            _frameRecorder = frameRecorder;
        }

        // Runs on the frame writer thread.
        private void FrameRecorder_FrameRecordingStarted()
        {
            AudioRecorder? audioRecorder = null;
            try
            {
                audioRecorder = new AudioRecorder(_microphoneIndex, _audioFilePath!, _recordSpeaker);
                audioRecorder.StartRecording();
                _audioRecorder = audioRecorder;
            }
            catch (Exception ex)
            {
                audioRecorder?.Dispose();
                _audioStartError = ex;
            }
        }

        /// <summary>
        /// Stops recording and writes the final MP4 file (H.264 video, AAC audio).
        /// </summary>
        public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var frameRecorder = _frameRecorder ?? throw new InvalidOperationException("Recording has not been started.");
            _frameRecorder = null;

            try
            {
                // Joining the recording threads can take a moment, so keep it off the caller's (UI) thread.
                await Task.Run(() =>
                {
                    // After this returns the writer thread has finished, so _audioRecorder/_audioStartError are final.
                    frameRecorder.StopRecording();

                    var audioRecorder = _audioRecorder;
                    _audioRecorder = null;
                    try
                    {
                        audioRecorder?.StopRecording();
                    }
                    finally
                    {
                        audioRecorder?.Dispose();
                    }
                }, CancellationToken.None).ConfigureAwait(false);

                if (_audioStartError is { } audioError)
                    throw new InvalidOperationException("Audio recording could not be started.", audioError);

                if (!File.Exists(_frameFilePath))
                    throw new InvalidOperationException("No frames were captured from the camera.");

                string? audioPath = File.Exists(_audioFilePath) ? _audioFilePath : null;
                await _ffmpeg.RunAsync(FFmpegRunner.BuildMergeArguments(_frameFilePath!, audioPath, _outputFilePath!), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                frameRecorder.Dispose();
                DeleteTempFiles();
            }
        }

        private void DeleteTempFiles()
        {
            foreach (var path in new[] { _frameFilePath, _audioFilePath })
            {
                if (path is null)
                    continue;

                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>
        /// Returns a copy of the latest camera frame while recording (an empty <see cref="Mat"/> until the first frame
        /// arrives). The caller owns the returned <see cref="Mat"/> and should dispose it.
        /// </summary>
        public Mat GetCurrentFrame()
        {
            var frameRecorder = _frameRecorder ?? throw new InvalidOperationException("Recording has not been started.");
            return frameRecorder.GetFrame();
        }

        public List<CameraDevice> GetCameraDevices()
        {
            return _camera.GetCameraDevices();
        }

        public List<Resolution> GetAvailableResolutions(string deviceName)
        {
            return _camera.GetAvailableResolutions(deviceName);
        }

        public List<InputAudioDevice> GetInputAudioDevices()
        {
            return AudioRecorder.GetInputAudioDevices();
        }
    }
}
