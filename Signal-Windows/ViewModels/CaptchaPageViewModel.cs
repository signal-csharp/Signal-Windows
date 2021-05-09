using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Views;
using Windows.UI.Core;

namespace Signal_Windows.ViewModels
{
    public class CaptchaPageViewModel : ViewModelBase
    {
        private readonly ILogger logger = LibsignalLogging.CreateLogger<CaptchaPageViewModel>();
        public CaptchaPage View { get; set; }

        // The webview source cannot be null so set it to the blank page.
        private Uri webViewSource = new Uri("about:blank");
        public Uri WebViewSource
        {
            get { return webViewSource; }
            set { webViewSource = value; RaisePropertyChanged(nameof(WebViewSource)); }
        }

        public void OnNavigatedTo()
        {
            Utils.EnableBackButton(BackButton_Click);
            WebViewSource = new Uri("https://signalcaptchas.org/registration/generate.html");
        }

        public void OnNavigatingFrom()
        {
            Utils.DisableBackButton(BackButton_Click);
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            View.Frame.GoBack();
            e.Handled = true;
        }

        public void SetToken(string signalCaptchaToken)
        {
            var registerPageInstance = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance;
            registerPageInstance.CaptchaCode = signalCaptchaToken;
            registerPageInstance.CaptchaWebViewEnabled = false;
            App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.CaptchaPageInstance.View.Frame.GoBack();
        }
    }
}
