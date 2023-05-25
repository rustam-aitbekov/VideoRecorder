using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VideoRecorder.Core
{
    internal class AudioRecorder : IDisposable
    {
        private bool _disposed;

        private WasapiLoopbackCapture speakerSource;
        private WaveFileWriter speakerWriter;

        private WaveInEvent microphoneSource;
        private WaveFileWriter microphoneWriter;

        private readonly string speakerWavFilePath;
        private readonly string microphoneWavFilePath;
        private readonly string audioFileSavePath;
        private bool recordSpeaker;

        string tempFilePath = Path.GetTempPath();

        public AudioRecorder(int deviceIndex, string audioFileSavePath, bool recordSpeaker = true)
        {
            this.audioFileSavePath = audioFileSavePath;
            this.recordSpeaker = recordSpeaker;

            microphoneWavFilePath = this.audioFileSavePath;

            if (this.recordSpeaker)
            {
                //Speaker
                speakerSource = new WasapiLoopbackCapture();
                speakerSource.DataAvailable += SourceSpeakers_DataAvailable;

                speakerWavFilePath = Path.Combine(tempFilePath, $"audio_from_speaker_{DateTime.Now.ToString("yyyyMMddHHmmss")}.wav");
                speakerWriter = new WaveFileWriter(speakerWavFilePath, speakerSource.WaveFormat);

                microphoneWavFilePath = Path.Combine(tempFilePath, $"audio_from_microphone_{DateTime.Now.ToString("yyyyMMddHHmmss")}.wav");
            }

            //Microphone
            microphoneSource = new WaveInEvent { WaveFormat = speakerSource.WaveFormat };
            microphoneSource.DataAvailable += SourceMicrophone_DataAvailable;
            microphoneSource.DeviceNumber = deviceIndex;
            
            microphoneWriter = new WaveFileWriter(microphoneWavFilePath, microphoneSource.WaveFormat);
        }

        ~AudioRecorder() 
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
                    microphoneWriter?.Dispose();
                    microphoneWriter = null;
                    microphoneSource?.Dispose();

                    if (recordSpeaker)
                    {
                        speakerWriter?.Dispose();
                        speakerWriter = null;
                        speakerSource.Dispose();
                    }
                }

                _disposed = true;
            }
        }

        private void CompileAudioFiles()
        {
            using (var speakerReader = new AudioFileReader(speakerWavFilePath))
            using (var microphoneReader = new AudioFileReader(microphoneWavFilePath))
            {
                speakerReader.Volume = 0.50f;
                microphoneReader.Volume = 1f;

                var mixer = new MixingSampleProvider(new[] { speakerReader, microphoneReader });
                WaveFileWriter.CreateWaveFile16(audioFileSavePath, mixer);
            }

            if (!string.IsNullOrEmpty(speakerWavFilePath))
            {
                File.Delete(speakerWavFilePath);
            }

            if (!string.IsNullOrEmpty(microphoneWavFilePath))
            {
                File.Delete(microphoneWavFilePath);
            }
        }

        private void SourceSpeakers_DataAvailable(object? sender, WaveInEventArgs e)
        {
            speakerWriter?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void SourceMicrophone_DataAvailable(object? sender, WaveInEventArgs e)
        {
            microphoneWriter?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        public static List<InputAudioDevice> GetInputAudioDevices()
        {
            List<InputAudioDevice> devices = new();

            for (int n = 0; n < WaveInEvent.DeviceCount; n++)
            {
                var caps = WaveInEvent.GetCapabilities(n);

                devices.Add(new InputAudioDevice() { Id = n, Name = caps.ProductName});
            }

            return devices;
        }

        public void StartRecording()
        {
            Parallel.Invoke
            (
                () => { microphoneSource.StartRecording(); },
                () => { if(recordSpeaker) speakerSource.StartRecording(); }
            );
        }

        public void StopRecording()
        {
            Parallel.Invoke
            (
                () => { microphoneSource.StopRecording(); },
                () => { if (recordSpeaker) speakerSource.StopRecording(); }
            );

            Dispose(true);

            if (recordSpeaker)
            {
                CompileAudioFiles();
            }
        }
    }

    public class InputAudioDevice
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }
}