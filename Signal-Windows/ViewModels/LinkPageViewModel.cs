using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.push;
using libsignalservice.util;
using Signal_Windows.Signal;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class LinkPageViewModel : ViewModelBase
    {
        public static ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;

        public LinkPage View;
        private static string URL = "https://textsecure-service.whispersystems.org";
        private SignalServiceUrl[] serviceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };
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
                    SignalServiceAccountManager accountManager = new SignalServiceAccountManager(serviceUrls, CancelSource.Token, "Signal-Windows");
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
                    LocalSettings.Values["Username"] = result.Number;

                    new Manager(password, (uint)registrationId, result, tmpSignalingKey);
                    Debug.WriteLine("success!");
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
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
