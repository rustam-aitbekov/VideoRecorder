using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using VideoRecorder.Exceptions;

namespace VideoRecorder
{
    /// <summary>
    /// Finds ffmpeg, or downloads a Windows build (with libx264) the first time it is needed.
    /// Builds come from https://github.com/BtbN/FFmpeg-Builds (GPL builds) and are verified against
    /// the SHA-256 checksums published with them.
    /// </summary>
    public static partial class FFmpegDownloader
    {
        private const string ReleaseBaseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";
        private const string MasterBuildFileName = "ffmpeg-master-latest-win64-gpl.zip";

        private static readonly HttpClient Http = CreateHttpClient();

        /// <summary>%LOCALAPPDATA%\VideoRecorder\ffmpeg</summary>
        public static string DefaultInstallFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoRecorder", "ffmpeg");

        /// <summary>Returns the full path of ffmpeg.exe if it is on PATH, otherwise <c>null</c>.</summary>
        public static string? FindOnPath()
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var candidate = Path.Combine(folder.Trim('"'), "ffmpeg.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException)
                {
                    // Invalid characters in a PATH entry.
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the path of ffmpeg.exe in <paramref name="installFolder"/>, downloading it first if it is not there yet.
        /// The download is about 200 MB and happens only once.
        /// </summary>
        /// <param name="installFolder">Where ffmpeg.exe is kept. Defaults to <see cref="DefaultInstallFolder"/>.</param>
        /// <param name="progress">Receives download progress from 0 to 1.</param>
        /// <exception cref="FFmpegException">The download failed or the file did not match its checksum.</exception>
        public static async Task<string> EnsureAsync(string? installFolder = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            installFolder ??= DefaultInstallFolder;
            string ffmpegPath = Path.Combine(installFolder, "ffmpeg.exe");
            if (File.Exists(ffmpegPath))
            {
                progress?.Report(1);
                return ffmpegPath;
            }

            Directory.CreateDirectory(installFolder);

            string id = Guid.NewGuid().ToString("N");
            string zipPath = Path.Combine(installFolder, $"download_{id}.zip.part");
            string exePartPath = Path.Combine(installFolder, $"ffmpeg_{id}.exe.part");

            try
            {
                string checksums = await GetStringAsync(ReleaseBaseUrl + "checksums.sha256", cancellationToken).ConfigureAwait(false);
                var (fileName, expectedSha256) = SelectBuild(checksums);

                await DownloadFileAsync(ReleaseBaseUrl + fileName, zipPath, progress, cancellationToken).ConfigureAwait(false);

                string actualSha256;
                await using (var zipStream = File.OpenRead(zipPath))
                {
                    actualSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(zipStream, cancellationToken).ConfigureAwait(false));
                }

                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new FFmpegException($"The downloaded ffmpeg build '{fileName}' does not match its published SHA-256 checksum.");

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                        ?? throw new FFmpegException($"'{fileName}' does not contain bin/ffmpeg.exe.");
                    entry.ExtractToFile(exePartPath, overwrite: true);
                }

                // Move into place only once complete, so an interrupted download is never mistaken for a working ffmpeg.
                File.Move(exePartPath, ffmpegPath, overwrite: true);
                progress?.Report(1);
                return ffmpegPath;
            }
            finally
            {
                TryDelete(zipPath);
                TryDelete(exePartPath);
            }
        }

        /// <summary>
        /// Picks the newest stable release build (ffmpeg-nX.Y-latest-win64-gpl-X.Y.zip) from the published
        /// checksums file, falling back to the master build if no release build is listed.
        /// </summary>
        internal static (string FileName, string Sha256) SelectBuild(string checksums)
        {
            (string FileName, string Sha256, Version Version)? bestRelease = null;
            (string FileName, string Sha256)? master = null;

            foreach (var rawLine in checksums.Split('\n'))
            {
                var match = ChecksumLine().Match(rawLine.Trim());
                if (!match.Success)
                    continue;

                string sha256 = match.Groups["sha"].Value.ToLowerInvariant();
                string fileName = match.Groups["file"].Value;

                var release = ReleaseBuildName().Match(fileName);
                if (release.Success)
                {
                    var version = new Version(int.Parse(release.Groups["major"].Value), int.Parse(release.Groups["minor"].Value));
                    if (bestRelease is null || version > bestRelease.Value.Version)
                        bestRelease = (fileName, sha256, version);
                }
                else if (fileName == MasterBuildFileName)
                {
                    master = (fileName, sha256);
                }
            }

            if (bestRelease is { } best)
                return (best.FileName, best.Sha256);
            if (master is { } m)
                return m;

            throw new FFmpegException("No suitable Windows ffmpeg build was found in the published checksums.");
        }

        [GeneratedRegex(@"^(?<sha>[0-9a-fA-F]{64})\s+\*?(?<file>\S+)$")]
        private static partial Regex ChecksumLine();

        [GeneratedRegex(@"^ffmpeg-n(?<major>\d+)\.(?<minor>\d+)-latest-win64-gpl-\d+\.\d+\.zip$")]
        private static partial Regex ReleaseBuildName();

        private static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                return await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new FFmpegException($"Could not download ffmpeg: {ex.Message}", ex);
            }
        }

        private static async Task DownloadFileAsync(string url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                long? total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long received = 0;
                int lastPercent = -1;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;

                    if (total > 0)
                    {
                        int percent = (int)(received * 100 / total.Value);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress?.Report(Math.Min(received / (double)total.Value, 1));
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new FFmpegException($"Could not download ffmpeg: {ex.Message}", ex);
            }
        }

        private static HttpClient CreateHttpClient()
        {
            // The download is large; rely on the caller's CancellationToken instead of the default 100 s timeout.
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoRecorder");
            return client;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
