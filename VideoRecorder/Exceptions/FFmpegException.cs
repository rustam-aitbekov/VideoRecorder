namespace VideoRecorder.Exceptions
{
    /// <summary>
    /// Thrown when ffmpeg cannot be started or exits with an error.
    /// </summary>
    public class FFmpegException : Exception
    {
        /// <summary>The ffmpeg exit code, or <c>null</c> if the process could not be started.</summary>
        public int? ExitCode { get; }

        /// <inheritdoc />
        public FFmpegException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public FFmpegException(string message, Exception? innerException) : base(message, innerException)
        {
        }

        public FFmpegException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }
    }
}
