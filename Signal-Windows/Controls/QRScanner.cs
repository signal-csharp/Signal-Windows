using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using System.Threading;
using Windows.Media.Capture;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Media.Devices;
using Windows.Devices.Enumeration;
using ZXing;
using libsignalservice;
using Microsoft.Extensions.Logging;

namespace Signal_Windows.Controls
{

    // this Control draw heavaly form the [Barcode_Scanner_UWP](https://github.com/programmersommer/Barcode_Scanner_UWP) sample.
    public sealed partial class QRScanner : UserControl, IDisposable
    {

        private static readonly ILogger Logger = LibsignalLogging.CreateLogger<QRScanner>();

        public event Action<string> CodeFound;
        public event Action Cancled;
        public event Action<Exception> Error;

        private MediaCapture mediaCapture;
        private readonly DispatcherTimer timerFocus;
        private readonly SemaphoreSlim videoCaptureSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim scanSemaphore = new SemaphoreSlim(1);
        private bool timerCaptureInProgress = false;

        private double width = 640;
        private double height = 480;
        private bool isReady = true;
        private bool isInitialized = false;
        private bool isScanning = true;

        private BarcodeReader _ZXingReader;
        private bool disposedValue;

        public QRScanner()
        {
            this.InitializeComponent();

            this.timerFocus = new DispatcherTimer();
        }

        #region capturing photo

        private async Task InitCamera()
        {
            if (this.isInitialized == true)
                return;

            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            var rearCamera = devices.FirstOrDefault(x => x.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Back);

            try
            {

                if (rearCamera != null)
                {
                    await this.mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = rearCamera.Id });
                }

                this.isInitialized = true;
                await this.SetResolution();
                if (this.mediaCapture.VideoDeviceController.FlashControl.Supported)
                    this.mediaCapture.VideoDeviceController.FlashControl.Auto = false;
            }
            catch { }
        }

        private async Task SetResolution()
        {
            System.Collections.Generic.IReadOnlyList<IMediaEncodingProperties> res;
            res = this.mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
            uint maxResolution = 0;
            var indexMaxResolution = 0;

            if (res.Count >= 1)
            {
                for (var i = 0; i < res.Count; i++)
                {
                    var vp = (VideoEncodingProperties)res[i];

                    if (vp.Width > maxResolution)
                    {
                        indexMaxResolution = i;
                        maxResolution = vp.Width;
                        this.width = vp.Width;
                        this.height = vp.Height;
                    }
                }
                await this.mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, res[indexMaxResolution]);
            }
        }


        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.SetGridSize();
        }


        private void SetGridSize()
        {
            this.VideoCaptureElement.Height = this.previewGrid.Height - 100;
            this.VideoCaptureElement.Width = this.previewGrid.Width;
        }

        #endregion


        #region Barcode scanner

        private async void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Cleanup();
            Cancled?.Invoke();
        }


        public async Task Cleanup()
        {
            if (!this.isReady)
            {
                this.isScanning = false;
                this.timerFocus.Stop();
                this.timerFocus.Tick -= this.timerFocus_Tick;

                await this.mediaCapture.StopPreviewAsync();
                this.mediaCapture.FocusChanged -= this.mediaCaptureManager_FocusChanged;

                this.isReady = true;
            }
        }

        private async Task StartPreview()
        {
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile")
            {
                this.mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
                this.mediaCapture.SetPreviewMirroring(true);
            }

            var focusControl = this.mediaCapture.VideoDeviceController.FocusControl;
            if (focusControl.Supported && focusControl.FocusChangedSupported)
            {

                var state = focusControl.FocusState;
                var mode = focusControl.Mode;
                var presset = focusControl.Preset;
                var modes = focusControl.SupportedFocusModes.ToArray();
                var presets = focusControl.SupportedPresets.ToArray();



                this.isReady = false;
                this.isScanning = true;

                this.mediaCapture.FocusChanged += this.mediaCaptureManager_FocusChanged;
                this.VideoCaptureElement.Source = this.mediaCapture;
                this.VideoCaptureElement.Stretch = Stretch.UniformToFill;
                await this.mediaCapture.StartPreviewAsync();
                await focusControl.UnlockAsync();
                var settings = new FocusSettings { Mode = FocusMode.Continuous, AutoFocusRange = AutoFocusRange.FullRange };
                focusControl.Configure(settings);
                await focusControl.FocusAsync();
            }
            else if (focusControl.Supported)
            {
                this.isReady = false;
                this.isScanning = true;

                this.VideoCaptureElement.Source = this.mediaCapture;
                this.VideoCaptureElement.Stretch = Stretch.UniformToFill;
                await this.mediaCapture.StartPreviewAsync();
                await focusControl.UnlockAsync();

                focusControl.Configure(new FocusSettings { Mode = FocusMode.Auto });
                this.timerFocus.Tick += this.timerFocus_Tick;
                this.timerFocus.Interval = new TimeSpan(0, 0, 3);
                this.timerFocus.Start();
            }
            else
            {
                var capabilits = this.mediaCapture.VideoDeviceController.Focus.Capabilities;
                await this.OnErrorAsync(new NotSupportedException("AutoFocus control is not supported on this device"));
            }


        }

        private async void mediaCaptureManager_FocusChanged(MediaCapture sender, MediaCaptureFocusChangedEventArgs args)
        {
            if (this.isScanning)
                await this.CapturePhotoFromCameraAsync();
        }

        private async void timerFocus_Tick(object sender, object e)
        {
            if (this.timerCaptureInProgress)
                return; // if camera is still focusing

            if (this.isScanning)
            {
                this.timerCaptureInProgress = true;

                await this.mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
                await this.CapturePhotoFromCameraAsync();

                this.timerCaptureInProgress = false;
            }
        }

        private async Task CapturePhotoFromCameraAsync()
        {
            if (!this.isScanning)
                return;

            if (await this.videoCaptureSemaphore.WaitAsync(0) == true)
            {
                try
                {
                    var videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)this.width, (int)this.height);
                    await this.mediaCapture.GetPreviewFrameAsync(videoFrame);

                    var bytes = await this.SaveSoftwareBitmapToBufferAsync(videoFrame.SoftwareBitmap);
                    await this.ScanImageAsync(bytes);
                }
                finally
                {
                    this.videoCaptureSemaphore.Release();
                }
            }
        }

        private async Task<byte[]> SaveSoftwareBitmapToBufferAsync(SoftwareBitmap softwareBitmap)
        {
            byte[] bytes = null;

            try
            {
                IRandomAccessStream stream = new InMemoryRandomAccessStream();

                // Create an encoder with the desired format
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.IsThumbnailGenerated = false;
                await encoder.FlushAsync();

                bytes = new byte[stream.Size];

                // This returns IAsyncOperationWithProgess, so you can add additional progress handling
                await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, Windows.Storage.Streams.InputStreamOptions.None);
            }

            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }

            return bytes;
        }


        private async Task BarCodeFound(string barcode)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.timerFocus.Stop();
                this.isScanning = false;

                if (barcode != null)
                {
                    CodeFound?.Invoke(barcode);
                }
            });
        }


        private async Task OnErrorAsync(Exception e)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => this.Error?.Invoke(e));

        }

        private async Task ScanImageAsync(byte[] pixelsArray)
        {
            await this.scanSemaphore.WaitAsync();
            try
            {
                if (this.isScanning)
                {

                    var result = this._ZXingReader.Decode(pixelsArray, (int)this.width, (int)this.height, RGBLuminanceSource.BitmapFormat.Unknown);
                    if (result != null)
                    {
                        await this.BarCodeFound(result.Text);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
            finally
            {
                this.scanSemaphore.Release();
            }
        }

        #endregion


        private async void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            await this.Cleanup();
        }


        public async Task StartScan()
        {
            this.cancleButton.IsEnabled = false;
            this.mediaCapture = new MediaCapture();
            this.isInitialized = false;
            this.isScanning = false;

            await this.InitCamera();

            if (this.isInitialized == false)
                return;


            this._ZXingReader = new BarcodeReader()
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions() { TryHarder = false, PossibleFormats = new BarcodeFormat[] { BarcodeFormat.All_1D, BarcodeFormat.QR_CODE } }
            };

            await this.StartPreview();
            this.cancleButton.IsEnabled = true;
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.videoCaptureSemaphore.Dispose();
                    this.scanSemaphore.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
