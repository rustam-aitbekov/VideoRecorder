using System.Runtime.InteropServices;
using DirectShowLib;

namespace VideoRecorder.Core
{
    internal sealed class Camera : IDisposable
    {
        private readonly DsDevice[] _dsDevices;

        public Camera()
        {
            _dsDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
        }

        public void Dispose()
        {
            foreach (var device in _dsDevices)
                device.Dispose();
        }

        /// <summary>
        /// Lists the resolutions the camera's capture pin offers, largest first.
        /// Returns an empty list if the camera is unknown or cannot be queried.
        /// </summary>
        public List<Resolution> GetAvailableResolutions(string deviceName)
        {
            DsDevice? device = _dsDevices.FirstOrDefault(x => x.Name.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));
            if (device is null)
                return new List<Resolution>();

            var resolutions = new List<Resolution>();

            IFilterGraph2? filterGraph = null;
            IBaseFilter? sourceFilter = null;
            IPin? capturePin = null;
            IEnumMediaTypes? mediaTypeEnum = null;

            try
            {
                filterGraph = (IFilterGraph2)new FilterGraph();
                int hr = filterGraph.AddSourceFilterForMoniker(device.Mon, null, device.Name, out sourceFilter);
                DsError.ThrowExceptionForHR(hr);

                capturePin = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);
                if (capturePin is null)
                    return resolutions;

                hr = capturePin.EnumMediaTypes(out mediaTypeEnum);
                DsError.ThrowExceptionForHR(hr);

                var mediaTypes = new AMMediaType[1];

                // Next returns S_OK (0) while it fetched a media type, S_FALSE (1) at the end.
                while (mediaTypeEnum.Next(1, mediaTypes, IntPtr.Zero) == 0 && mediaTypes[0] is { } mediaType)
                {
                    try
                    {
                        if (TryGetResolution(mediaType, out var resolution) && !resolutions.Contains(resolution))
                        {
                            resolutions.Add(resolution);
                        }
                    }
                    finally
                    {
                        DsUtils.FreeAMMediaType(mediaType);
                        mediaTypes[0] = null!;
                    }
                }
            }
            catch (COMException)
            {
                // The device is busy, was unplugged, or does not expose a capture pin.
                return new List<Resolution>();
            }
            finally
            {
                Release(mediaTypeEnum);
                Release(capturePin);
                Release(sourceFilter);
                Release(filterGraph);
            }

            return resolutions.OrderByDescending(x => x.Width).ThenByDescending(x => x.Height).ToList();
        }

        private static bool TryGetResolution(AMMediaType mediaType, out Resolution resolution)
        {
            resolution = new Resolution();
            if (mediaType.formatPtr == IntPtr.Zero)
                return false;

            BitmapInfoHeader header;
            if (mediaType.formatType == FormatType.VideoInfo && mediaType.formatSize >= Marshal.SizeOf<VideoInfoHeader>())
            {
                var info = new VideoInfoHeader();
                Marshal.PtrToStructure(mediaType.formatPtr, info);
                header = info.BmiHeader;
            }
            else if (mediaType.formatType == FormatType.VideoInfo2 && mediaType.formatSize >= Marshal.SizeOf<VideoInfoHeader2>())
            {
                var info = new VideoInfoHeader2();
                Marshal.PtrToStructure(mediaType.formatPtr, info);
                header = info.BmiHeader;
            }
            else
            {
                return false;
            }

            if (header.Size == 0 || header.BitCount == 0 || header.Width == 0 || header.Height == 0)
                return false;

            // A negative height only means the image is stored top-down.
            resolution = new Resolution { Width = header.Width, Height = Math.Abs(header.Height) };
            return true;
        }

        private static void Release(object? comObject)
        {
            if (comObject is not null && Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
        }

        public List<CameraDevice> GetCameraDevices()
        {
            List<CameraDevice> devices = new();

            for (int i = 0; i < _dsDevices.Length; i++)
            {
                if (!_dsDevices[i].Name.Contains("virtual", StringComparison.OrdinalIgnoreCase))
                {
                    // Id is the DirectShow device index, which is what StartRecording expects.
                    devices.Add(new CameraDevice() { Id = i, Name = _dsDevices[i].Name });
                }
            }

            return devices;
        }
    }

    public class CameraDevice
    {
        /// <summary>The camera index to pass to <see cref="Video.StartRecording"/>.</summary>
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        public override bool Equals(object? obj)
        {
            return obj is Resolution item && item.Width == Width && item.Height == Height;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }
    }
}
