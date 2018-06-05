using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class LinkPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<LinkPageViewModel>();
        public LinkPage View;
        private CancellationTokenSource CancelSource;
        private bool UIEnabled = true;
        private Visibility _QRVisible;

        public Visibility QRVisible
        {
            get
            {
                return _QRVisible;
            }
            set
            {
                _QRVisible = value;
                RaisePropertyChanged("QRVisible");
            }
        }

        private string _QRCodeString;

        public string QRCodeString
        {
            get
            {
                return _QRCodeString;
            }
            set
            {
                _QRCodeString = value;
                RaisePropertyChanged(nameof(QRCodeString));
            }
        }

        public async Task OnNavigatedTo()
        {
            UIEnabled = true;
            QRVisible = Visibility.Collapsed;
            QRCodeString = "";

            Utils.EnableBackButton(BackButton_Click);
            await BeginLinking();
        }

        public async Task BeginLinking()
        {
            try
            {
                CancelSource = new CancellationTokenSource();
                // clean the database from stale values
                await Task.Run(() =>
                {
                    LibsignalDBContext.PurgeAccountData();
                });

                (string password, IdentityKeyPair tmpIdentity) = await Task.Run(() =>
                {
                    string newPassword = Base64.EncodeBytes(Util.GetSecretBytes(18));
                    IdentityKeyPair newTmpIdentity = KeyHelper.generateIdentityKeyPair();
                    return (newPassword, newTmpIdentity);
                });

                // fetch new device uuid
                SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceConfiguration, "Signal-Windows");
                string uuid = await accountManager.GetNewDeviceUuid(CancelSource.Token);
                string tsdevice = "tsdevice:/?uuid=" + Uri.EscapeDataString(uuid) + "&pub_key=" + Uri.EscapeDataString(Base64.EncodeBytesWithoutPadding(tmpIdentity.getPublicKey().serialize()));

                View.SetQR(tsdevice); //TODO generate qrcode in worker task
                QRVisible = Visibility.Visible;
                QRCodeString = tsdevice;

                string tmpSignalingKey = Base64.EncodeBytes(Util.GetSecretBytes(52));
                int registrationId = (int)KeyHelper.generateRegistrationId(false);

                var provisionMessage = await accountManager.GetProvisioningMessage(CancelSource.Token, tmpIdentity);
                int deviceId = await accountManager.FinishNewDeviceRegistration(CancelSource.Token, provisionMessage, tmpSignalingKey, password, false, true, registrationId, View.GetDeviceName());
                SignalStore store = new SignalStore()
                {
                    DeviceId = (uint)deviceId,
                    IdentityKeyPair = Base64.EncodeBytes(provisionMessage.Identity.serialize()),
                    NextSignedPreKeyId = 1,
                    Password = password,
                    PreKeyIdOffset = 1,
                    Registered = true,
                    RegistrationId = (uint)registrationId,
                    SignalingKey = tmpSignalingKey,
                    Username = provisionMessage.Number
                };
                await Task.Run(() =>
                {
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);
                });

                // reload registered state
                UIEnabled = false;
                App.Handle.Store = store;

                // create prekeys
                await LibsignalDBContext.RefreshPreKeys(CancelSource.Token, new SignalServiceAccountManager(App.ServiceConfiguration, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));

                // reload again with prekeys and their offsets
                App.Handle.Store = LibsignalDBContext.GetSignalStore();
                await View.Finish(true);
            }
            catch (Exception e)
            {
                var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                Logger.LogError("BeginLinking() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
            }
        }

        internal async void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            if (UIEnabled)
            {
                CancelSource.Cancel();
                await Task.Run(() =>
                {
                    App.Handle.PurgeAccountData();
                });
                View.Frame.GoBack();
                e.Handled = true;
            }
        }
    }
}