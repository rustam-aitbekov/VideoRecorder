using DirectShowLib;
using System.Text;

namespace VideoRecorder.Exceptions
{
    internal static class ThrowHelper
    {
        public static void ThrowDeviceNotFoundException(string deviceName)
        {
            var sb = new StringBuilder($"The device with the name '{deviceName}' could not be found. The following devices are available: ");
            var inputDeviceNames = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).Select(x => x.Name).ToArray();
            sb.Append(string.Join(", ", inputDeviceNames));
            throw new DeviceNotFoundException(sb.ToString(), null, deviceName);
        }

        public static void ThrowVideoCaptureNotReadyException()
        {
            throw new FrameCaptureNotReadyException();
        }
    }
}