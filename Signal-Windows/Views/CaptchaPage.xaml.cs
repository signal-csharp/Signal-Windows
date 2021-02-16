using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Signal_Windows.ViewModels;
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
    // Pretty much the only reason this page exists is because putting the WebView on the RegisterPage wouldn't
    // correctly load the CAPTCHA part of the web page for me.
    public sealed partial class CaptchaPage : Page
    {
        public CaptchaPageViewModel Vm
        {
            get
            {
                return (CaptchaPageViewModel)DataContext;
            }
        }

        public CaptchaPage()
        {
            // pretty much the only reason 
            this.InitializeComponent();
            Vm.View = this;
            KeyDown += CaptchaPage_KeyDown;
        }

        private bool IsCtrlKeyPressed()
        {
            var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(Windows.System.VirtualKey.Control);
            return (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private void CaptchaPage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if ((IsCtrlKeyPressed() && e.Key == Windows.System.VirtualKey.R) ||
                e.Key == Windows.System.VirtualKey.F5)
            {
                webView.Refresh();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Vm.OnNavigatedTo();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Vm.OnNavigatingFrom();
        }
    }
}
