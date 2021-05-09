using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Signal_Windows.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DeviceSettingsPage : Page
    {
        public DeviceSettingsPage()
        {
            this.InitializeComponent();
        }

        public DeviceSettingsPageViewmodel Vm
        {
            get { return (DeviceSettingsPageViewmodel)this.DataContext; }
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Utils.EnableBackButton(this.BackButton_Click);
            this.Vm.OnNavigatedTo();
            Application.Current.Suspending += this.App_Suspending;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            Utils.DisableBackButton(this.BackButton_Click);
            Application.Current.Suspending -= this.App_Suspending;
        }

        private async void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await this.scanner.Cleanup();
            this.ScannerPopup.IsOpen = false;
            deferral.Complete();
        }


        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            this.Frame.GoBack();
            e.Handled = true;
        }


        private async void CodeFound(string barcode)
        {
            this.ScannerPopup.IsOpen = false;
            await this.Vm.AddDevice(new Uri(barcode));
        }

        private void OnError(Exception e)
        {

        }

        private async void startScanButton_Click(object sender, RoutedEventArgs e)
        {
            ScannerPopup.IsOpen = true;
            await this.scanner.StartScan();
        }

        private void root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.scanner.Width = this.root.ActualWidth;
            this.scanner.Height = this.root.ActualHeight;
        }

        private void scanner_Cancled()
        {
            this.ScannerPopup.IsOpen = false;

        }
    }
}
