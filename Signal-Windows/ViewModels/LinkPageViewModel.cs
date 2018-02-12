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
        private Task LinkingTask;

        public string DeviceName { get; set; } = "Signal on Windows";

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
                string deviceName = DeviceName;
                LinkingTask = Task.Run(() =>
                {
                    /* clean the database from stale values */
                    LibsignalDBContext.PurgeAccountData();

                    /* prepare qrcode */
                    string password = Base64.encodeBytes(Util.getSecretBytes(18));
                    IdentityKeyPair tmpIdentity = KeyHelper.generateIdentityKeyPair();
                    SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceUrls, CancelSource.Token, "Signal-Windows");
                    string uuid = accountManager.GetNewDeviceUuid(CancelSource.Token);
                    string tsdevice = "tsdevice:/?uuid=" + Uri.EscapeDataString(uuid) + "&pub_key=" + Uri.EscapeDataString(Base64.encodeBytesWithoutPadding(tmpIdentity.getPublicKey().serialize()));
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        View.SetQR(tsdevice);
                        QRVisible = Visibility.Visible;
                        QRCodeString = tsdevice;
                    }).AsTask().Wait();

                    string tmpSignalingKey = Base64.encodeBytes(Util.getSecretBytes(52));
                    int registrationId = (int)KeyHelper.generateRegistrationId(false);

                    NewDeviceLinkResult result = accountManager.FinishNewDeviceRegistration(tmpIdentity, tmpSignalingKey, password, false, true, registrationId, deviceName);
                    SignalStore store = new SignalStore()
                    {
                        DeviceId = (uint)result.DeviceId,
                        IdentityKeyPair = Base64.encodeBytes(result.Identity.serialize()),
                        NextSignedPreKeyId = 1,
                        Password = password,
                        PreKeyIdOffset = 1,
                        Registered = true,
                        RegistrationId = (uint)registrationId,
                        SignalingKey = tmpSignalingKey,
                        Username = result.Number
                    };
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);

                    /* reload registered state */
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        UIEnabled = false;
                        App.Store = store;
                        App.Handle.Store = store;
                    }).AsTask().Wait();

                    /* create prekeys */
                    LibsignalDBContext.RefreshPreKeys(new SignalServiceAccountManager(App.ServiceUrls, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));

                    /* reload again with prekeys and their offsets */
                    store = LibsignalDBContext.GetSignalStore();
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                        App.Handle.Store = store;
                        View.Finish(true);
                    }).AsTask().Wait();
                });
                await LinkingTask;
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
                if (LinkingTask != null)
                {
                    try
                    {
                        await LinkingTask;
                    }
                    catch (Exception ex)
                    {
                        var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                        Logger.LogError("BackButton_Click() failed in line {0}: {1}\n{2}", line, ex.Message, ex.StackTrace);
                    }
                }
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