using OpenCvSharp;
using VideoRecorder.Core;

namespace VideoRecorder
{
    public class Video : IDisposable
    {
        private bool _disposed;

        private FrameRecorder frameRecorder;
        private AudioRecorder audioRecorder;
        private Camera camera = new();

        private string? frameFilePath;
        private string? audioFilePath;
        private string? compiledFilePath;
        private int microphoneIndex;
        private bool recordSpeaker;

        string tempFilePath = Path.GetTempPath();

        public Video(string ffmpegPath)
        {
            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegPath);

            frameFilePath = Path.Combine(tempFilePath, $"temp_frames_{DateTime.Now.ToString("yyyyMMddHHmmss")}.mp4");
            audioFilePath = Path.Combine(tempFilePath, $"temp_audio_{DateTime.Now.ToString("yyyyMMddHHmmss")}.wav");
        }

        ~Video() 
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
                    frameRecorder.Dispose();
                    audioRecorder.Dispose();
                }

                _disposed = true;
            }
        }

        private void DeleteFiles()
        {
            if (File.Exists(frameFilePath))
            {
                File.Delete(frameFilePath);
            }

            if (File.Exists(audioFilePath))
            {
                File.Delete(audioFilePath);
            }
        }

        private async Task AddAudioToVideoAsync()
        {
            string arguments = $@"-i ""{frameFilePath}"" -i ""{audioFilePath}"" -c:v copy -map 0:v -map 1:a -y ""{compiledFilePath}""";

            await Xabe.FFmpeg.FFmpeg.Conversions.New().Start(arguments);
        }

        public void StartRecording(int cameraIndex, Resolution resolution, int fps, int microphoneIndex, bool recordSpeaker, string filePath)
        {
            compiledFilePath = filePath;
            this.microphoneIndex = microphoneIndex;
            this.recordSpeaker = recordSpeaker;

            frameRecorder = new(cameraIndex, resolution.Width, resolution.Height, fps);
            frameRecorder.FrameRecordingStarted += FrameRecorder_FrameRecordingStarted;

            frameRecorder.StartRecording(frameFilePath);
        }

        private void FrameRecorder_FrameRecordingStarted()
        {
            audioRecorder = new(microphoneIndex, audioFilePath, recordSpeaker);
            audioRecorder.StartRecording();
        }

        public async Task StopRecordingAsync()
        {
            frameRecorder.StopRecording();
            audioRecorder.StopRecording();

            await AddAudioToVideoAsync();

            DeleteFiles();
        }

        public Mat GetCurrentFrame()
        {
            return frameRecorder.GetFrame();
        }

        public List<CameraDevice> GetCameraDevices()
        {
            return camera.GetCameraDevices();
        }

        public List<Resolution> GetAvailableResolutions(string deviceName)
        {
            return camera.GetAvailableResolutions(deviceName);
        }

        public List<InputAudioDevice> GetInputAudioDevices()
        {
            return AudioRecorder.GetInputAudioDevices();
        }        
    }
}