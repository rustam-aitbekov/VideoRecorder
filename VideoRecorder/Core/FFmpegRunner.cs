using System.ComponentModel;
using System.Diagnostics;
using VideoRecorder.Exceptions;

namespace VideoRecorder.Core
{
    /// <summary>
    /// Runs the ffmpeg executable directly (no third-party wrapper).
    /// </summary>
    internal sealed class FFmpegRunner
    {
        public string Executable { get; }

        /// <param name="ffmpegPath">
        /// A folder containing ffmpeg.exe, the full path to the ffmpeg executable,
        /// or a command name such as "ffmpeg" that is resolved through PATH.
        /// </param>
        public FFmpegRunner(string ffmpegPath)
        {
            Executable = ResolveExecutable(ffmpegPath);
        }

        internal static string ResolveExecutable(string? ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath))
                return "ffmpeg";

            if (Directory.Exists(ffmpegPath))
                return Path.Combine(ffmpegPath, "ffmpeg.exe");

            return ffmpegPath;
        }

        /// <summary>
        /// Arguments that encode the captured frames to H.264 and, when present, mux in the recorded audio as AAC.
        /// </summary>
        internal static IReadOnlyList<string> BuildMergeArguments(string videoPath, string? audioPath, string outputPath)
        {
            var args = new List<string> { "-y", "-nostdin", "-hide_banner", "-loglevel", "error", "-i", videoPath };

            if (audioPath is not null)
                args.AddRange(new[] { "-i", audioPath });

            args.AddRange(new[] { "-map", "0:v:0" });
            if (audioPath is not null)
                args.AddRange(new[] { "-map", "1:a:0" });

            args.AddRange(new[] { "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-pix_fmt", "yuv420p" });
            if (audioPath is not null)
                args.AddRange(new[] { "-c:a", "aac", "-b:a", "192k" });

            args.AddRange(new[] { "-movflags", "+faststart", outputPath });
            return args;
        }

        public async Task RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo(Executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                throw new FFmpegException(
                    $"Could not start ffmpeg ('{Executable}'). Pass the folder or full path of ffmpeg.exe to the Video constructor, or add ffmpeg to PATH.",
                    ex);
            }

            // Read both streams so ffmpeg never blocks on a full pipe.
            var stdErrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var stdOutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already exited */ }
                throw;
            }

            string stdErr = await stdErrTask.ConfigureAwait(false);
            await stdOutTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new FFmpegException(
                    $"ffmpeg exited with code {process.ExitCode}: {stdErr.Trim()}",
                    process.ExitCode);
            }
        }
    }
}
