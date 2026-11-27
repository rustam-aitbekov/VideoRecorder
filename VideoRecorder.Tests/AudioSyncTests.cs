using VideoRecorder.Core;

namespace VideoRecorder.Tests;

public class AudioSyncTests
{
    private const int SampleRate = 48000;
    private const long Tolerance = SampleRate / 10; // 100 ms

    [Fact]
    public void NoSilenceWhenStreamIsOnTime()
    {
        long result = AudioSync.SilenceFramesToInsert(TimeSpan.FromSeconds(1), SampleRate, writtenFrames: 47520, incomingFrames: 480, Tolerance);

        Assert.Equal(0, result);
    }

    [Fact]
    public void SmallGapsAreTreatedAsLatency()
    {
        // 50 ms behind: within the tolerance.
        long result = AudioSync.SilenceFramesToInsert(TimeSpan.FromSeconds(1), SampleRate, writtenFrames: 45120, incomingFrames: 480, Tolerance);

        Assert.Equal(0, result);
    }

    [Fact]
    public void FillsGapLeftByLoopbackSilence()
    {
        // Nothing played for the first 3 seconds, then a 10 ms packet arrives at t = 3 s.
        long result = AudioSync.SilenceFramesToInsert(TimeSpan.FromSeconds(3), SampleRate, writtenFrames: 0, incomingFrames: 480, Tolerance);

        Assert.Equal(3 * SampleRate - 480, result);
    }

    [Fact]
    public void PadsTrackToFullLengthAtStop()
    {
        long result = AudioSync.SilenceFramesToInsert(TimeSpan.FromSeconds(5), SampleRate, writtenFrames: 2 * SampleRate, incomingFrames: 0, toleranceFrames: 0);

        Assert.Equal(3 * SampleRate, result);
    }

    [Fact]
    public void NeverReturnsNegativeWhenAhead()
    {
        long result = AudioSync.SilenceFramesToInsert(TimeSpan.FromSeconds(1), SampleRate, writtenFrames: 2 * SampleRate, incomingFrames: 480, toleranceFrames: 0);

        Assert.Equal(0, result);
    }
}
