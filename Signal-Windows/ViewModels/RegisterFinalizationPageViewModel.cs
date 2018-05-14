using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class RegisterFinalizationPageViewModel : ViewModelBase
    {
        public RegisterFinalizationPage View { get; set; }

        private string _VerificationCode = string.Empty;
        public string VerificationCode
        {
            get { return _VerificationCode; }
            set { _VerificationCode = value; RaisePropertyChanged(nameof(VerificationCode)); }
        }

        internal SignalServiceAccountManager AccountManager { get; private set; }
        internal string Password { get; private set; }
        internal uint SignalRegistrationId { get; private set; }
        internal IdentityKeyPair IdentityKeyPair { get; private set; }
        private CancellationTokenSource CancelSource;
        private bool _UIEnabled;
        public bool UIEnabled
        {
            get
            {
                return _UIEnabled;
            }
            set
            {
                _UIEnabled = value;
                RaisePropertyChanged(nameof(UIEnabled));
            }
        }

        internal void FinishButton_Click()
        {
            View.Frame.Navigate(typeof(FinishRegistrationPage));
        }

        internal async Task OnNavigatedTo()
        {
            UIEnabled = false;
            Utils.EnableBackButton(BackButton_Click);
            CancelSource = new CancellationTokenSource();
            Password = Base64.EncodeBytes(Util.getSecretBytes(18));
            SignalRegistrationId = KeyHelper.generateRegistrationId(false);
            IdentityKeyPair = KeyHelper.generateIdentityKeyPair();
            try
            {
                AccountManager = await Task.Run(() => InitRegistration(false));
                UIEnabled = true;
            }
            catch(Exception e)
            {
                var title = e.Message;
                var content = "Please ensure your phone number is correct and your device is connected to the internet.";
                MessageDialog dialog = new MessageDialog(content, title);
                var result = dialog.ShowAsync();
                View.Frame.Navigate(typeof(RegisterPage));
            }
        }

        private SignalServiceAccountManager InitRegistration(bool voice)
        {
            App.Handle.PurgeAccountData();
            SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceConfiguration, App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.FinalNumber, Password, 1 /*device id isn't actually used*/, App.USER_AGENT);
            if (voice)
            {
                accountManager.RequestVoiceVerificationCode();
            }
            else
            {
                accountManager.RequestSmsVerificationCode();
            }
            return accountManager;
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            if(UIEnabled)
            {
                View.Frame.GoBack();
                e.Handled = true;
            }
        }

        internal void OnNavigatingFrom()
        {
            Utils.DisableBackButton(BackButton_Click);
        }
    }
}
