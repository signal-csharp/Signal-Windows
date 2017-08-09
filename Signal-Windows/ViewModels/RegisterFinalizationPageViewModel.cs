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

        private SignalServiceAccountManager AccountManager;
        private string Password;
        private uint SignalRegistrationId;
        private IdentityKeyPair IdentityKeyPair;
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

        internal async void FinishButton_Click()
        {
            try
            {
                await Task.Run(() =>
                {
                    string SignalingKey = Base64.encodeBytes(Util.getSecretBytes(52));
                    AccountManager.verifyAccountWithCode(VerificationCode, SignalingKey, SignalRegistrationId, false, false, true);
                    SignalStore store = new SignalStore()
                    {
                        DeviceId = 1,
                        IdentityKeyPair = Base64.encodeBytes(IdentityKeyPair.serialize()),
                        NextSignedPreKeyId = 1,
                        Password = Password,
                        PreKeyIdOffset = 1,
                        Registered = true,
                        RegistrationId = SignalRegistrationId,
                        SignalingKey = SignalingKey,
                        Username = App.ViewModels.RegisterPageInstance.FinalNumber,
                    };
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                    }).AsTask().Wait();

                    /* create prekeys */
                    LibsignalDBContext.RefreshPreKeys(new SignalServiceAccountManager(App.ServiceUrls, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));

                    /* reload again with prekeys and their offsets */
                    store = LibsignalDBContext.GetSignalStore();
                    Debug.WriteLine("success!");
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                    }).AsTask().Wait();
                });
                View.Frame.Navigate(typeof(MainPage));
            }
            catch (Exception e)
            {
                var title = "Verification failed";
                var content = "Please enter the correct verification code.";
                MessageDialog dialog = new MessageDialog(content, title);
                var result = dialog.ShowAsync();
                View.Frame.Navigate(typeof(RegisterPage));
            }
        }

        internal async Task OnNavigatedTo()
        {
            UIEnabled = false;
            Utils.EnableBackButton(BackButton_Click);
            CancelSource = new CancellationTokenSource();
            Password = Base64.encodeBytes(Util.getSecretBytes(18));
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
            LibsignalDBContext.PurgeAccountData();
            SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceUrls, App.ViewModels.RegisterPageInstance.FinalNumber, Password, 1, App.USER_AGENT);
            if (voice)
            {
                accountManager.requestVoiceVerificationCode();
            }
            else
            {
                accountManager.requestSmsVerificationCode();
            }
            return accountManager;
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            if(UIEnabled)
            {
                View.Frame.GoBack();
            }
        }

        internal void OnNavigatingFrom()
        {
            Utils.DisableBackButton(BackButton_Click);
        }
    }
}
