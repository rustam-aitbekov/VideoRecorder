using System;
using OpenCvSharp.WpfExtensions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VideoRecorder.Core;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace VideoRecorder.Wpf.Sample
{
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker frameBkgWorker;
        private readonly Video video;

        private string fileSavePath;

        public MainWindow()
        {
            InitializeComponent();

            frameBkgWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            frameBkgWorker.DoWork += Worker_DoWork;

            video = new(@"D:\FFmpeg");

            List<Device> devices = video.GetAvailableDevices();

            CmbBoxCamera.ItemsSource = devices;
            CmbBoxCamera.SelectedIndex = 0;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            while (!worker.CancellationPending)
            {
                Dispatcher.Invoke(() =>
                {
                    FrameImage.Source = video.GetCurrentFrame().ToWriteableBitmap();
                });

                Thread.Sleep(30);
            }
        }

        private void BtnFileSavePath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFileDlg = new();
            var result = openFileDlg.ShowDialog();
            if (result.ToString() != string.Empty)
            {
                BtnFileSavePath.Content = openFileDlg.SelectedPath;
                fileSavePath = openFileDlg.SelectedPath;

                BtnStartRecord.IsEnabled = true;
            }
        }

        private void BtnStartRecord_Click(object sender, RoutedEventArgs e)
        {
            string videoFileSavePath = Path.Combine(fileSavePath, $"video_{DateTime.Now.ToString("yyyyMMddHHmmss")}.mp4");
            Resolution resolution = (Resolution)CmbBoxCameraResolution.SelectedItem;

            video.StartRecording(CmbBoxCamera.SelectedIndex, new Resolution() { Width = resolution.Width, Height = resolution .Height}, 60, videoFileSavePath);

            frameBkgWorker.RunWorkerAsync();

            BtnStopRecord.IsEnabled = true;
        }

        private async void BtnStopRecord_Click(object sender, RoutedEventArgs e)
        {
            await video.StopRecordingAsync();

            frameBkgWorker.CancelAsync();
        }

        private void CmbBoxCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Device selectedItem = (Device)CmbBoxCamera.SelectedItem;

            CmbBoxCameraResolution.ItemsSource = video.GetAvailableResolutions(selectedItem.DeviceName);
            CmbBoxCameraResolution.SelectedIndex = 0;
        }
    }
}
