using VideoRecorder.Core;

namespace VideoRecorder.Tests;

public class VideoTests
{
    [Fact]
    public async Task StopWithoutStartThrows()
    {
        using var video = new Video();

        await Assert.ThrowsAsync<InvalidOperationException>(() => video.StopRecordingAsync());
    }

    [Fact]
    public void GetCurrentFrameWithoutStartThrows()
    {
        using var video = new Video();

        Assert.Throws<InvalidOperationException>(() => video.GetCurrentFrame());
    }

    [Fact]
    public void StartValidatesArguments()
    {
        using var video = new Video();
        var resolution = new Resolution { Width = 640, Height = 480 };

        Assert.Throws<ArgumentNullException>(() => video.StartRecording(0, null!, 30, 0, false, "out.mp4"));
        Assert.Throws<ArgumentOutOfRangeException>(() => video.StartRecording(0, resolution, 0, 0, false, "out.mp4"));
        Assert.Throws<ArgumentException>(() => video.StartRecording(0, resolution, 30, 0, false, " "));
        Assert.False(video.IsRecording);
    }

    [Fact]
    public void UnknownCameraHasNoResolutions()
    {
        using var video = new Video();

        Assert.Empty(video.GetAvailableResolutions("no such camera " + Guid.NewGuid()));
    }

    [Fact]
    public void DisposedVideoThrows()
    {
        var video = new Video();
        video.Dispose();

        Assert.Throws<ObjectDisposedException>(() => video.StartRecording(0, new Resolution { Width = 1, Height = 1 }, 30, 0, false, "out.mp4"));
    }
}

public class ResolutionTests
{
    [Fact]
    public void EqualResolutionsHaveEqualHashCodes()
    {
        var a = new Resolution { Width = 1920, Height = 1080 };
        var b = new Resolution { Width = 1920, Height = 1080 };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<Resolution> { a, b });
    }

    [Fact]
    public void DifferentResolutionsAreNotEqual()
    {
        Assert.NotEqual(new Resolution { Width = 1920, Height = 1080 }, new Resolution { Width = 1080, Height = 1920 });
        Assert.False(new Resolution().Equals(null));
    }

    [Fact]
    public void FormatsAsWidthByHeight()
    {
        Assert.Equal("1280x720", new Resolution { Width = 1280, Height = 720 }.ToString());
    }
}
