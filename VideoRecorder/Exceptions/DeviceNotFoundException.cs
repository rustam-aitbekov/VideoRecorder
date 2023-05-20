namespace VideoRecorder.Exceptions
{
    public class DeviceNotFoundException : Exception
    {
        public string DeviceName { get; }

        /// <inheritdoc />
        public DeviceNotFoundException()
        {
        }

        /// <inheritdoc />
        public DeviceNotFoundException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public DeviceNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public DeviceNotFoundException(string message, string deviceName) : base(message)
        {
            DeviceName = deviceName;
        }

        public DeviceNotFoundException(string message, Exception innerException, string deviceName) : base(message, innerException)
        {
            DeviceName = deviceName;
        }
    }
}
