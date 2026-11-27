## Simple video Recording lib for .NET 10, C#

It's my first open source library. Feel free to ask questions, create pull requests or fork.

Records a camera together with the microphone (and, optionally, whatever the speakers are playing) into an MP4 file (H.264 video, AAC audio). Windows only.

## Important
The library calls [ffmpeg](https://ffmpeg.org/download.html) directly to produce the final MP4. Use a build that includes **libx264**, for example the "full" builds from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) or the "gpl" builds from [BtbN](https://github.com/BtbN/FFmpeg-Builds/releases).

Tell the library where ffmpeg is:

```csharp
Video video = new(@"D:\FFmpeg");                 // folder that contains ffmpeg.exe
Video video = new(@"D:\FFmpeg\bin\ffmpeg.exe");  // or the executable itself
Video video = new();                             // or use ffmpeg from PATH
```

Or let the library download it the first time (about 200 MB, once). It picks the newest stable Windows GPL build from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), checks it against the published SHA-256 checksum and keeps only `ffmpeg.exe` in `%LOCALAPPDATA%\VideoRecorder\ffmpeg`:

```csharp
string ffmpeg = FFmpegDownloader.FindOnPath()
    ?? await FFmpegDownloader.EnsureAsync(progress: new Progress<double>(p => Console.WriteLine($"{p:P0}")));

Video video = new(ffmpeg);
```

Note that these ffmpeg builds are licensed under the GPL; they are downloaded by the end user's machine and are not part of this package.

## Usage

```csharp
using Video video = new(@"D:\FFmpeg");

CameraDevice camera = video.GetCameraDevices()[0];
Resolution resolution = video.GetAvailableResolutions(camera.Name)[0];
InputAudioDevice microphone = video.GetInputAudioDevices()[0];

video.StartRecording(camera.Id, resolution, fps: 30, microphone.Id, recordSpeaker: true, @"C:\Videos\video.mp4");

// Optional live preview: GetCurrentFrame returns a copy, dispose it when done.
using (Mat frame = video.GetCurrentFrame()) { /* show frame */ }

await video.StopRecordingAsync();
```

- Use `CameraDevice.Id` / `InputAudioDevice.Id` (not the position in a list) when starting a recording.
- The video always runs at the requested frame rate and matches real time; if the camera delivers fewer frames, the latest frame is repeated, so audio and video stay in sync.
- Speaker audio stays in sync even when nothing is playing for a while.

When the sample WPF app opens, it uses the `FFMPEG_PATH` environment variable (folder or ffmpeg.exe) if set, then a previously downloaded copy, then ffmpeg on PATH, and otherwise downloads ffmpeg (progress is shown at the bottom of the window).

## Sample wpf app:

![image](https://github.com/rustam-aytbekoff/VideoRecorder/assets/45448359/efe72f33-49cd-44b9-88e4-3aed7b8fbbbc)

## Important Nuget packages
- [DirectShowLib.Standard](https://www.nuget.org/packages/DirectShowLib.Standard)
- [NAudio](https://www.nuget.org/packages/NAudio)
- [OpenCvSharp4.Extensions](https://www.nuget.org/packages/OpenCvSharp4.Extensions)
- [OpenCvSharp4.WpfExtensions](https://www.nuget.org/packages/OpenCvSharp4.WpfExtensions)
- [OpenCvSharp4.runtime.win](https://www.nuget.org/packages/OpenCvSharp4.runtime.win)

## Tested devices

- A4tech FHD 1080P Camera
- Razer KIYO Pro Camera
- HyperX Quadcast Microphone

## License

This library is distributed under the Apache License 2.0 License found in the [LICENSE](./LICENSE) file.
