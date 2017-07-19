using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class LinkPageViewModel : ViewModelBase
    {
        public LinkPage View;
        private CancellationTokenSource CancelSource;
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

        public void Init()
        {
            QRVisible = Visibility.Collapsed;
        }

        public void BeginLinking()
        {
            CancelSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                try
                {
                    string password = Base64.encodeBytes(Util.getSecretBytes(18));
                    IdentityKeyPair tmpIdentity = KeyHelper.generateIdentityKeyPair();
                    SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceUrls, CancelSource.Token, "Signal-Windows");
                    string uuid = accountManager.GetNewDeviceUuid(CancelSource.Token);
                    Debug.WriteLine("received uuid=" + uuid);
                    string tsdevice = "tsdevice:/?uuid=" + Uri.EscapeDataString(uuid) + "&pub_key=" + Uri.EscapeDataString(Base64.encodeBytesWithoutPadding(tmpIdentity.getPublicKey().serialize()));
                    Debug.WriteLine(tsdevice);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        View.SetQR(tsdevice);
                        QRVisible = Visibility.Visible;
                    }).AsTask().Wait();

                    string tmpSignalingKey = Base64.encodeBytes(Util.getSecretBytes(52));
                    int registrationId = (int)KeyHelper.generateRegistrationId(false);
                    string deviceName = "windowstest";

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
                    SignalDBContext.SaveOrUpdateSignalStore(store);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                    }).AsTask().Wait();
                    SignalDBContext.RefreshPreKeys(new SignalServiceAccountManager(App.ServiceUrls, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));
                    store = SignalDBContext.GetSignalStore(); /* reload after prekey changes */
                    Debug.WriteLine("success!");
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                        View.Finish(true);
                    }).AsTask().Wait();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            });
        }
    }
}