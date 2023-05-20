using OpenCvSharp;
using VideoRecorder.Core;

namespace VideoRecorder
{
    public class Video
    {
        private FrameRecorder frameRecorder;
        private AudioRecorder soundRecorder;
        private Camera camera;

        private string? frameFilePath;
        private string? audioFilePath;
        private string? compiledFilePath;

        string tempFilePath = Path.GetTempPath();

        public Video(string ffmpegPath)
        {
            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegPath);

            frameFilePath = Path.Combine(tempFilePath, $"temp_frames_{DateTime.Now.ToString("yyyyMMddHHmmss")}.mp4");
            audioFilePath = Path.Combine(tempFilePath, $"temp_audio_{DateTime.Now.ToString("yyyyMMddHHmmss")}.wav");

            camera = new Camera();
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

        public void StartRecording(int deviceIndex, Resolution resolution, int fps, string filePath)
        {
            compiledFilePath = filePath;

            frameRecorder = new(deviceIndex, resolution.Width, resolution.Height, fps);
            frameRecorder.FrameRecordingStarted += FrameRecorder_FrameRecordingStarted;

            frameRecorder.StartRecording(frameFilePath);
        }

        private void FrameRecorder_FrameRecordingStarted()
        {
            soundRecorder = new(audioFilePath);
            soundRecorder.StartRecording();
        }

        public async Task StopRecordingAsync()
        {
            frameRecorder.StopRecording();
            soundRecorder.StopRecording();

            await AddAudioToVideoAsync();

            DeleteFiles();
        }

        public Mat GetCurrentFrame()
        {
            return frameRecorder.GetFrame();
        }

        public List<Device> GetAvailableDevices()
        {
            return camera.GetAvailableDevices();
        }
        public List<Resolution> GetAvailableResolutions(string deviceName)
        {
            return camera.GetAvailableResolutions(deviceName);
        }
    }
}