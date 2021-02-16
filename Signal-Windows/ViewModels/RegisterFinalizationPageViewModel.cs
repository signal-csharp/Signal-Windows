using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
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
        private readonly ILogger logger = LibsignalLogging.CreateLogger<RegisterFinalizationPageViewModel>();
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

        internal void OnNavigatedTo()
        {
            UIEnabled = false;
            Utils.EnableBackButton(BackButton_Click);
        }

        internal async Task RegisterFinalizationPage_Loaded()
        {
            CancelSource = new CancellationTokenSource();
            Password = Base64.EncodeBytes(Util.GetSecretBytes(18));
            SignalRegistrationId = KeyHelper.generateRegistrationId(false);
            IdentityKeyPair = KeyHelper.generateIdentityKeyPair();
            try
            {
                AccountManager = await InitRegistration(false);
                UIEnabled = true;
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(), ex, ex.Message);
                View.Frame.GoBack();
            }
        }

        private async Task<SignalServiceAccountManager> InitRegistration(bool voice)
        {
            App.Handle.PurgeAccountData();
            SignalServiceAccountManager accountManager = new SignalServiceAccountManager(LibUtils.ServiceConfiguration,
                App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.FinalNumber,
                Password, 1 /*device id isn't actually used*/, LibUtils.USER_AGENT, LibUtils.HttpClient);

            string captcha = null;
            if (!string.IsNullOrWhiteSpace(App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.CaptchaCode))
            {
                captcha = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.CaptchaCode;
            }

            try
            {
                if (voice)
                {
                    await accountManager.RequestVoiceVerificationCode(captcha, CancelSource.Token);
                }
                else
                {
                    await accountManager.RequestSmsVerificationCode(captcha, CancelSource.Token);
                }
            }
            catch (CaptchaRequiredException)
            {
                App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.CaptchaWebViewEnabled = true;
                throw;
            }
            catch (Exception ex)
            {
                await Utils.CallOnMainViewUIThreadAsync(async () =>
                {
                    var title = ex.Message;
                    var content = "Please ensure your phone number is correct and your device is connected to the internet.";
                    MessageDialog dialog = new MessageDialog(content, title);
                    await dialog.ShowAsync();
                });
                throw;
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
