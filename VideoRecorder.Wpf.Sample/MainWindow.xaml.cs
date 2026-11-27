using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp.WpfExtensions;
using VideoRecorder.Core;

namespace VideoRecorder.Wpf.Sample
{
    public partial class MainWindow : Window
    {
        private const int RecordingFps = 30;

        private readonly DispatcherTimer previewTimer;
        private readonly CancellationTokenSource closingCts = new();
        private Video? video;

        private WriteableBitmap? previewBitmap;
        private string? fileSavePath;
        private bool isStopping;

        public MainWindow()
        {
            InitializeComponent();

            previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / RecordingFps) };
            previewTimer.Tick += PreviewTimer_Tick;

            UpdateControls();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string ffmpegPath;
            try
            {
                ffmpegPath = await FindOrDownloadFFmpegAsync(closingCts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // window closed during the download
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "ffmpeg is not available. Set FFMPEG_PATH to an ffmpeg folder or ffmpeg.exe and restart.";
                MessageBox.Show(this, ex.Message, "Could not download ffmpeg", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            TxtStatus.Text = $"ffmpeg: {ffmpegPath}";
            video = new Video(ffmpegPath);

            CmbBoxCamera.ItemsSource = video.GetCameraDevices();
            CmbBoxCamera.SelectedIndex = 0;

            CmbBoxMicrophone.ItemsSource = video.GetInputAudioDevices();
            CmbBoxMicrophone.SelectedIndex = 0;

            UpdateControls();
        }

        /// <summary>
        /// Uses FFMPEG_PATH (folder or ffmpeg.exe) if set, then a previously downloaded copy, then ffmpeg on PATH,
        /// and otherwise downloads ffmpeg once into %LOCALAPPDATA%\VideoRecorder\ffmpeg.
        /// </summary>
        private async Task<string> FindOrDownloadFFmpegAsync(CancellationToken cancellationToken)
        {
            if (Environment.GetEnvironmentVariable("FFMPEG_PATH") is { Length: > 0 } configured)
                return configured;

            string downloaded = Path.Combine(FFmpegDownloader.DefaultInstallFolder, "ffmpeg.exe");
            if (File.Exists(downloaded))
                return downloaded;

            if (FFmpegDownloader.FindOnPath() is { } onPath)
                return onPath;

            TxtStatus.Text = "Downloading ffmpeg (one-time, about 200 MB)...";
            var progress = new Progress<double>(p => TxtStatus.Text = $"Downloading ffmpeg (one-time, about 200 MB)... {p:P0}");
            return await FFmpegDownloader.EnsureAsync(progress: progress, cancellationToken: cancellationToken);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            closingCts.Cancel();
            previewTimer.Stop();
            video?.Dispose();
        }

        private void PreviewTimer_Tick(object? sender, EventArgs e)
        {
            if (video is null || !video.IsRecording)
                return;

            using var frame = video.GetCurrentFrame();
            if (frame.Empty())
                return;

            if (previewBitmap is null || previewBitmap.PixelWidth != frame.Width || previewBitmap.PixelHeight != frame.Height)
            {
                previewBitmap = frame.ToWriteableBitmap();
                FrameImage.Source = previewBitmap;
            }
            else
            {
                WriteableBitmapConverter.ToWriteableBitmap(frame, previewBitmap);
            }
        }

        private void BtnFileSavePath_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog openFolderDlg = new();
            if (openFolderDlg.ShowDialog() == true)
            {
                BtnFileSavePath.Content = openFolderDlg.FolderName;
                fileSavePath = openFolderDlg.FolderName;
            }

            UpdateControls();
        }

        private void BtnStartRecord_Click(object sender, RoutedEventArgs e)
        {
            if (video is null
                || fileSavePath is null
                || CmbBoxCamera.SelectedItem is not CameraDevice camera
                || CmbBoxCameraResolution.SelectedItem is not Resolution resolution
                || CmbBoxMicrophone.SelectedItem is not InputAudioDevice microphone)
            {
                return;
            }

            string videoFileSavePath = Path.Combine(fileSavePath, $"video_{DateTime.Now:yyyyMMddHHmmss}.mp4");

            try
            {
                video.StartRecording(camera.Id, resolution, RecordingFps, microphone.Id, ChckBxRecordSpeaker.IsChecked == true, videoFileSavePath);
                previewTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start recording", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateControls();
        }

        private async void BtnStopRecord_Click(object sender, RoutedEventArgs e)
        {
            if (video is null)
                return;

            previewTimer.Stop();
            isStopping = true;
            UpdateControls();

            try
            {
                await video.StopRecordingAsync();
                MessageBox.Show(this, "The video has been saved.", "Recording finished", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not save the recording", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isStopping = false;
                UpdateControls();
            }
        }

        private void CmbBoxCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (video is not null && CmbBoxCamera.SelectedItem is CameraDevice selectedItem)
            {
                CmbBoxCameraResolution.ItemsSource = video.GetAvailableResolutions(selectedItem.Name);
                CmbBoxCameraResolution.SelectedIndex = 0;
            }

            UpdateControls();
        }

        private void Settings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateControls();
        }

        private void UpdateControls()
        {
            // Selection events can fire while InitializeComponent is still running.
            if (BtnStartRecord is null || BtnStopRecord is null)
                return;

            bool ready = video is not null;
            bool recording = video?.IsRecording == true;
            bool idle = ready && !recording && !isStopping;

            BtnStartRecord.IsEnabled = idle
                && fileSavePath is not null
                && CmbBoxCamera.SelectedItem is CameraDevice
                && CmbBoxCameraResolution.SelectedItem is Resolution
                && CmbBoxMicrophone.SelectedItem is InputAudioDevice;
            BtnStopRecord.IsEnabled = recording && !isStopping;

            CmbBoxCamera.IsEnabled = idle;
            CmbBoxCameraResolution.IsEnabled = idle;
            CmbBoxMicrophone.IsEnabled = idle;
            ChckBxRecordSpeaker.IsEnabled = idle;
            BtnFileSavePath.IsEnabled = idle;
        }
    }
}
