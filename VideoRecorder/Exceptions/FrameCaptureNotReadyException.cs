namespace VideoRecorder.Exceptions
{
    public class FrameCaptureNotReadyException : Exception
    {
        /// <inheritdoc />
        public FrameCaptureNotReadyException()
        {
        }

        /// <inheritdoc />
        public FrameCaptureNotReadyException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public FrameCaptureNotReadyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
