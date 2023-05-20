using System.Runtime.InteropServices;
using DirectShowLib;

namespace VideoRecorder.Core
{
    internal class Camera
    {
        private DsDevice[] _dsDevices;

        public Camera()
        {
            _dsDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
        }

        public List<Resolution> GetAvailableResolutions(string deviceName)
        {
            try
            {
                DsDevice device = _dsDevices.Where(x => x.Name.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                int hr, bitCount = 0;

                var m_FilterGraph2 = new FilterGraph() as IFilterGraph2;
                hr = m_FilterGraph2.AddSourceFilterForMoniker(device.Mon, null, device.Name, out IBaseFilter sourceFilter);

                var pRaw2 = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);
                List<Resolution> AvailableResolutions = new();

                var v = new VideoInfoHeader();
                hr = pRaw2.EnumMediaTypes(out IEnumMediaTypes mediaTypeEnum);

                AMMediaType[] mediaTypes = new AMMediaType[1];
                IntPtr fetched = IntPtr.Zero;
                hr = mediaTypeEnum.Next(1, mediaTypes, fetched);

                while (fetched != null && mediaTypes[0] != null)
                {
                    Marshal.PtrToStructure(mediaTypes[0].formatPtr, v);
                    if (v.BmiHeader.Size != 0 && v.BmiHeader.BitCount != 0)
                    {
                        if (v.BmiHeader.BitCount > bitCount)
                        {
                            bitCount = v.BmiHeader.BitCount;
                        }

                        Resolution resolution = new() { Width = v.BmiHeader.Width, Height = v.BmiHeader.Height };
                        if (!AvailableResolutions.Contains(resolution))
                        {
                            AvailableResolutions.Add(resolution);
                        }
                    }
                    hr = mediaTypeEnum.Next(1, mediaTypes, fetched);
                }

                var result = AvailableResolutions.OrderByDescending(x => x.Width).ThenByDescending(x => x.Height).ToList();

                return result;
            }
            catch
            {
                return new List<Resolution>();
            }
        }

        public List<Device> GetAvailableDevices()
        {
            List<Device> devices = new();

            for (int i = 0; i < _dsDevices.Length; i++)
            {
                if (!_dsDevices[i].Name.Contains("virtual", StringComparison.OrdinalIgnoreCase))
                {
                    devices.Add(new Device() { Id = i, DeviceName = _dsDevices[i].Name });
                }
            }

            return devices;
        }
    }

    public class Device
    {
        public int Id { get; set; }
        public string DeviceName { get; set; }
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
            return obj is Resolution item && item.ToString() == ToString();
        }
    }
}