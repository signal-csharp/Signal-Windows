using Signal_Windows.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZXing.Mobile;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace Signal_Windows.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class LinkPage : Page
    {
        public LinkPage()
        {
            this.InitializeComponent();
            Vm.View = this;
        }

        public LinkPageViewModel Vm
        {
            get
            {
                return (LinkPageViewModel)DataContext;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Utils.DisableBackButton();
            Vm.Init();
            Vm.BeginLinking();
        }

        public void SetQR(string qr)
        {
            var writer = new BarcodeWriter()
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new ZXing.Common.EncodingOptions
                {
                    Height = 300,
                    Width = 300
                },
                Renderer = new ZXing.Mobile.WriteableBitmapRenderer() { Foreground = Windows.UI.Colors.Black }
            };
            QRCode.Source = writer.Write(qr);
        }

        public void Finish(bool success)
        {
            if (success)
            {
                Frame.Navigate(typeof(MainPage));
            }
        }
    }
}