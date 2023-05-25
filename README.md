Simple Video Recoding lib for .NET 6

## Important
Need to download ffmpeg files(ffmpeg.exe, ffplay.exe, ffprobe.exe) and provide path into constructor while initializing:

```csharp
Video video = new(@"D:\FFmpeg");
```

Sample wpf app:

![image](https://github.com/rustam-aytbekoff/VideoRecorder/assets/45448359/efe72f33-49cd-44b9-88e4-3aed7b8fbbbc)

## Important Nuget packages
- [DirectShowLib.Standard](https://www.nuget.org/packages/DirectShowLib.Standard)
- [NAudio](https://www.nuget.org/packages/NAudio)
- [OpenCvSharp4.Extensions](https://www.nuget.org/packages/OpenCvSharp4.Extensions)
- [OpenCvSharp4.WpfExtensions](https://www.nuget.org/packages/OpenCvSharp4.WpfExtensions)
- [OpenCvSharp4.runtime.win](https://www.nuget.org/packages/OpenCvSharp4.runtime.win)
- [Xabe.FFmpeg](https://www.nuget.org/packages/Xabe.FFmpeg)

## Tested devices

- A4tech FHD 1080P Camera
- Razer KIYO Pro Camera
- HyperX Quadcast Microphone

## License

This library is distributed under the Apache License 2.0 License found in the [LICENSE](./LICENSE) file.
