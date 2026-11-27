using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VideoRecorder.Core
{
    /// <summary>
    /// Records the microphone and, optionally, what the speakers play (WASAPI loopback),
    /// then mixes both into a single WAV file.
    /// </summary>
    internal sealed class AudioRecorder : IDisposable
    {
        // 48 kHz / 16-bit / mono is accepted by practically every capture device through WinMM.
        // The microphone is converted to the speaker format when the two tracks are mixed.
        private static readonly WaveFormat MicrophoneFormat = new(48000, 16, 1);

        // Gaps in the loopback stream shorter than this are normal buffering latency, not silence.
        private static readonly TimeSpan SilenceGapTolerance = TimeSpan.FromMilliseconds(100);

        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
        private static readonly byte[] SilenceChunk = new byte[64 * 1024];

        private readonly object _speakerLock = new();
        private readonly object _microphoneLock = new();
        private readonly Stopwatch _clock = new();

        private WasapiRecorder? _speakerSource;
        private WaveFileWriter? _speakerWriter;
        private long _speakerFramesWritten;
        private readonly ManualResetEventSlim _speakerStopped = new(true);

        private WaveIn? _microphoneSource;
        private WaveFileWriter? _microphoneWriter;
        private readonly ManualResetEventSlim _microphoneStopped = new(true);

        private readonly string? _speakerWavFilePath;
        private readonly string _microphoneWavFilePath;
        private readonly string _audioFileSavePath;

        private bool _started;
        private bool _stopped;
        private bool _disposed;

        public AudioRecorder(int deviceIndex, string audioFileSavePath, bool recordSpeaker = true)
        {
            _audioFileSavePath = audioFileSavePath;
            _microphoneWavFilePath = audioFileSavePath;

            try
            {
                if (recordSpeaker)
                {
                    // Speaker: loopback capture of the default render device.
                    _speakerSource = new WasapiRecorderBuilder().WithLoopbackCapture().Build();
                    _speakerSource.DataAvailable += SourceSpeakers_DataAvailable;
                    _speakerSource.RecordingStopped += (_, _) => _speakerStopped.Set();

                    string id = Guid.NewGuid().ToString("N");
                    _speakerWavFilePath = Path.Combine(Path.GetTempPath(), $"videorecorder_speaker_{id}.wav");
                    _speakerWriter = new WaveFileWriter(_speakerWavFilePath, _speakerSource.WaveFormat);

                    _microphoneWavFilePath = Path.Combine(Path.GetTempPath(), $"videorecorder_microphone_{id}.wav");
                }

                // Microphone
                _microphoneSource = new WaveIn { DeviceNumber = deviceIndex, WaveFormat = MicrophoneFormat };
                _microphoneSource.DataAvailable += SourceMicrophone_DataAvailable;
                _microphoneSource.RecordingStopped += (_, _) => _microphoneStopped.Set();

                _microphoneWriter = new WaveFileWriter(_microphoneWavFilePath, _microphoneSource.WaveFormat);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            StopSources();
            ReleaseResources();

            // A recording that was never finished leaves only temporary files behind.
            if (!_stopped)
            {
                DeleteTempFiles();
            }

            _speakerStopped.Dispose();
            _microphoneStopped.Dispose();
        }

        public void StartRecording()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
                throw new InvalidOperationException("Audio recording has already been started.");
            _started = true;

            _clock.Start();

            _microphoneStopped.Reset();
            _microphoneSource!.StartRecording();

            if (_speakerSource is not null)
            {
                _speakerStopped.Reset();
                _speakerSource.StartRecording();
            }
        }

        public void StopRecording()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_started || _stopped)
                return;
            _stopped = true;

            StopSources();
            _clock.Stop();

            // Make the speaker track as long as the recording, even if it ended in silence.
            lock (_speakerLock)
            {
                if (_speakerWriter is not null)
                {
                    long padding = AudioSync.SilenceFramesToInsert(_clock.Elapsed, _speakerWriter.WaveFormat.SampleRate, _speakerFramesWritten, 0, 0);
                    WriteSilence(_speakerWriter, padding * _speakerWriter.WaveFormat.BlockAlign);
                }
            }

            ReleaseResources();

            if (_speakerWavFilePath is not null)
            {
                try
                {
                    CompileAudioFiles(_speakerWavFilePath);
                }
                finally
                {
                    DeleteTempFiles();
                }
            }
        }

        private void StopSources()
        {
            if (!_started)
                return;

            try { _microphoneSource?.StopRecording(); } catch (Exception) { /* device may already be gone */ }
            try { _speakerSource?.StopRecording(); } catch (Exception) { /* device may already be gone */ }

            // Let the last buffers arrive before the writers are closed.
            _microphoneStopped.Wait(StopTimeout);
            _speakerStopped.Wait(StopTimeout);
        }

        private void ReleaseResources()
        {
            lock (_microphoneLock)
            {
                _microphoneWriter?.Dispose();
                _microphoneWriter = null;
            }
            _microphoneSource?.Dispose();
            _microphoneSource = null;

            lock (_speakerLock)
            {
                _speakerWriter?.Dispose();
                _speakerWriter = null;
            }
            _speakerSource?.Dispose();
            _speakerSource = null;
        }

        private void DeleteTempFiles()
        {
            if (_speakerWavFilePath is null)
                return; // the microphone file is the final output in this case

            TryDelete(_speakerWavFilePath);
            TryDelete(_microphoneWavFilePath);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private void CompileAudioFiles(string speakerWavFilePath)
        {
            using var speakerReader = new AudioFileReader(speakerWavFilePath) { Volume = 0.5f };
            using var microphoneReader = new AudioFileReader(_microphoneWavFilePath) { Volume = 1f };

            int sampleRate = speakerReader.WaveFormat.SampleRate;
            int channels = Math.Min(speakerReader.WaveFormat.Channels, 2);

            var mixer = new MixingSampleProvider(new[]
            {
                AudioMixing.ConvertTo(speakerReader, sampleRate, channels),
                AudioMixing.ConvertTo(microphoneReader, sampleRate, channels),
            });

            WaveFileWriter.CreateWaveFile16(_audioFileSavePath, mixer);
        }

        private void SourceSpeakers_DataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
        {
            lock (_speakerLock)
            {
                var writer = _speakerWriter;
                if (writer is null)
                    return;

                var format = writer.WaveFormat;
                long incomingFrames = buffer.Length / format.BlockAlign;
                long toleranceFrames = (long)(SilenceGapTolerance.TotalSeconds * format.SampleRate);

                long silenceFrames = AudioSync.SilenceFramesToInsert(_clock.Elapsed, format.SampleRate, _speakerFramesWritten, incomingFrames, toleranceFrames);
                WriteSilence(writer, silenceFrames * format.BlockAlign);

                if (flags.HasFlag(AudioClientBufferFlags.Silent))
                {
                    // WASAPI asks us to treat the packet as silence regardless of the buffer contents.
                    WriteSilence(writer, buffer.Length);
                }
                else
                {
                    writer.Write(buffer);
                }

                _speakerFramesWritten += silenceFrames + incomingFrames;
            }
        }

        private static void WriteSilence(WaveFileWriter writer, long byteCount)
        {
            while (byteCount > 0)
            {
                int chunk = (int)Math.Min(byteCount, SilenceChunk.Length);
                writer.Write(SilenceChunk, 0, chunk);
                byteCount -= chunk;
            }
        }

        private void SourceMicrophone_DataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (_microphoneLock)
            {
                _microphoneWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }

        public static List<InputAudioDevice> GetInputAudioDevices()
        {
            List<InputAudioDevice> devices = new();

            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var caps = WaveIn.GetCapabilities(n);

                devices.Add(new InputAudioDevice() { Id = n, Name = caps.ProductName });
            }

            return devices;
        }
    }

    public class InputAudioDevice
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
