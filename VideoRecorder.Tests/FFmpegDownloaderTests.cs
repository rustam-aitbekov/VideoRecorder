using VideoRecorder.Exceptions;

namespace VideoRecorder.Tests;

[Collection(ProcessEnvironmentCollection.Name)]
public class FFmpegDownloaderTests
{
    private const string ShaA = "6a76339ee6f1760a8bbf16853c281efcda25d080a87d062c74a087cecb6e0bf4";
    private const string ShaB = "503a177dff3ecc70ecf0615387edcd42c60fa9b4df88d713aaee9050b00541b2";
    private const string ShaC = "a5dbf962654ffbdecbf6e4622d7f68e8c8b989ef8d6d9a2ede86e2bb7bff51c7";
    private const string ShaD = "02de0a3a0caa9d082e93a1dda2840ed9c7171c56721945eb1d19665dcb8a7923";

    [Fact]
    public void PicksNewestStableReleaseBuild()
    {
        string checksums = $"""
            {ShaA}  ffmpeg-master-latest-win64-gpl.zip
            {ShaB}  ffmpeg-n8.1-latest-win64-gpl-8.1.zip
            {ShaD}  ffmpeg-n9.0-latest-win64-gpl-shared-9.0.zip
            {ShaC}  ffmpeg-n9.0-latest-win64-gpl-9.0.zip
            {ShaA}  ffmpeg-n10.0-latest-linux64-gpl-10.0.tar.xz
            """;

        var (fileName, sha256) = FFmpegDownloader.SelectBuild(checksums);

        Assert.Equal("ffmpeg-n9.0-latest-win64-gpl-9.0.zip", fileName);
        Assert.Equal(ShaC, sha256);
    }

    [Fact]
    public void ComparesVersionsNumerically()
    {
        string checksums = $"{ShaB}  ffmpeg-n9.0-latest-win64-gpl-9.0.zip\r\n{ShaC}  ffmpeg-n10.1-latest-win64-gpl-10.1.zip\r\n";

        Assert.Equal("ffmpeg-n10.1-latest-win64-gpl-10.1.zip", FFmpegDownloader.SelectBuild(checksums).FileName);
    }

    [Fact]
    public void FallsBackToMasterBuild()
    {
        string checksums = $"{ShaA}  ffmpeg-master-latest-win64-gpl.zip\n{ShaD}  ffmpeg-master-latest-win64-gpl-shared.zip\n";

        Assert.Equal(("ffmpeg-master-latest-win64-gpl.zip", ShaA), FFmpegDownloader.SelectBuild(checksums));
    }

    [Fact]
    public void ThrowsWhenNoWindowsBuildIsListed()
    {
        Assert.Throws<FFmpegException>(() => FFmpegDownloader.SelectBuild($"{ShaA}  ffmpeg-master-latest-linux64-gpl.tar.xz\nnot a checksum line\n"));
    }

    [Fact]
    public async Task ExistingInstallIsReturnedWithoutDownloading()
    {
        string folder = Path.Combine(Path.GetTempPath(), "vr_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string exe = Path.Combine(folder, "ffmpeg.exe");
            await File.WriteAllTextAsync(exe, "stub");
            double? reported = null;

            string result = await FFmpegDownloader.EnsureAsync(folder, new SyncProgress(p => reported = p));

            Assert.Equal(exe, result);
            Assert.Equal(1, reported);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void FindOnPathFindsFfmpegExe()
    {
        string folder = Path.Combine(Path.GetTempPath(), "vr_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string? originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            File.WriteAllText(Path.Combine(folder, "ffmpeg.exe"), "stub");
            Environment.SetEnvironmentVariable("PATH", $"C:\\does-not-exist{Path.PathSeparator}\"{folder}\"");

            Assert.Equal(Path.Combine(folder, "ffmpeg.exe"), FFmpegDownloader.FindOnPath());

            Environment.SetEnvironmentVariable("PATH", "C:\\does-not-exist");
            Assert.Null(FFmpegDownloader.FindOnPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
