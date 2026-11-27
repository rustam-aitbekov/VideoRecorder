using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VideoRecorder.Core;

namespace VideoRecorder.Tests;

public class AudioMixingTests
{
    private static ISampleProvider Source(int sampleRate, int channels) => new SignalGenerator(sampleRate, channels) { Frequency = 440, Gain = 0.2 };

    [Fact]
    public void ReturnsSameProviderWhenFormatAlreadyMatches()
    {
        var source = Source(48000, 2);

        var result = AudioMixing.ConvertTo(source, 48000, 2);

        Assert.Same(source, result);
    }

    [Theory]
    [InlineData(48000, 1, 48000, 2)] // microphone (mono) mixed with stereo speakers
    [InlineData(48000, 1, 44100, 2)] // ...at a different sample rate
    [InlineData(44100, 2, 48000, 1)]
    [InlineData(48000, 6, 48000, 2)] // 5.1 speaker output
    public void ConvertsToRequestedFormat(int sourceRate, int sourceChannels, int targetRate, int targetChannels)
    {
        var result = AudioMixing.ConvertTo(Source(sourceRate, sourceChannels), targetRate, targetChannels);

        Assert.Equal(targetRate, result.WaveFormat.SampleRate);
        Assert.Equal(targetChannels, result.WaveFormat.Channels);

        var buffer = new float[targetChannels * 1000];
        Assert.Equal(buffer.Length, result.Read(buffer.AsSpan()));
    }

    [Fact]
    public void ConvertedSourcesCanBeMixed()
    {
        var speaker = Source(48000, 2);
        var microphone = AudioMixing.ConvertTo(Source(48000, 1), 48000, 2);

        // MixingSampleProvider throws if the formats differ.
        var mixer = new MixingSampleProvider(new[] { speaker, microphone });

        Assert.Equal(2, mixer.WaveFormat.Channels);
    }

    [Fact]
    public void RejectsUnsupportedChannelCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMixing.ConvertTo(Source(48000, 2), 48000, 6));
    }
}
