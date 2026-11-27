using VideoRecorder.Core;
using VideoRecorder.Exceptions;

namespace VideoRecorder.Tests;

[Collection(ProcessEnvironmentCollection.Name)]
public class FFmpegRunnerTests
{
    [Fact]
    public void FolderResolvesToFfmpegExeInside()
    {
        string folder = Path.GetTempPath();

        Assert.Equal(Path.Combine(folder, "ffmpeg.exe"), FFmpegRunner.ResolveExecutable(folder));
    }

    [Theory]
    [InlineData(@"C:\tools\ffmpeg\bin\ffmpeg.exe")]
    [InlineData("ffmpeg")]
    public void FileOrCommandIsUsedAsIs(string path)
    {
        Assert.Equal(path, FFmpegRunner.ResolveExecutable(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyPathFallsBackToPath(string? path)
    {
        Assert.Equal("ffmpeg", FFmpegRunner.ResolveExecutable(path));
    }

    [Fact]
    public void MergeArgumentsWithAudio()
    {
        var args = FFmpegRunner.BuildMergeArguments(@"C:\t\frames.avi", @"C:\t\audio.wav", @"C:\out\video with spaces.mp4");

        Assert.Equal(
            new[]
            {
                "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
                "-i", @"C:\t\frames.avi", "-i", @"C:\t\audio.wav",
                "-map", "0:v:0", "-map", "1:a:0",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "192k",
                "-movflags", "+faststart", @"C:\out\video with spaces.mp4",
            },
            args);
    }

    [Fact]
    public void MergeArgumentsWithoutAudio()
    {
        var args = FFmpegRunner.BuildMergeArguments("frames.avi", null, "out.mp4");

        Assert.DoesNotContain("1:a:0", args);
        Assert.DoesNotContain("-c:a", args);
        Assert.Equal(1, args.Count(a => a == "-i"));
        Assert.Equal("out.mp4", args[^1]);
    }

    [Fact]
    public async Task MissingExecutableGivesHelpfulError()
    {
        var runner = new FFmpegRunner(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ffmpeg.exe"));

        var ex = await Assert.ThrowsAsync<FFmpegException>(() => runner.RunAsync(new[] { "-version" }));

        Assert.Contains("Could not start ffmpeg", ex.Message);
        Assert.Null(ex.ExitCode);
    }

    [Fact]
    public async Task NonZeroExitCodeIsReported()
    {
        // cmd.exe stands in for ffmpeg: it exits with code 3 and writes to stderr.
        var runner = new FFmpegRunner("cmd.exe");

        var ex = await Assert.ThrowsAsync<FFmpegException>(() => runner.RunAsync(new[] { "/c", "echo boom 1>&2 & exit 3" }));

        Assert.Equal(3, ex.ExitCode);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task SuccessfulRunCompletes()
    {
        var runner = new FFmpegRunner("cmd.exe");

        await runner.RunAsync(new[] { "/c", "exit 0" });
    }
}
