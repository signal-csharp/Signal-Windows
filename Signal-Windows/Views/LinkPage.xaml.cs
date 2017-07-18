using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.push;
using libsignalservice.util;
using Newtonsoft.Json;
using Signal_Windows.Signal;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace Signal_Windows.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class LinkPage : Page
    {
        [JsonIgnore] public static ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        private static string URL = "https://textsecure-service.whispersystems.org";
        private SignalServiceUrl[] serviceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };

        public LinkPage()
        {
            this.InitializeComponent();
        }

        public LinkPageViewModel Vm
        {
            get
            {
                return (LinkPageViewModel)DataContext;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs navEvent)
        {
            base.OnNavigatedTo(navEvent);
            Task.Run(() =>
            {
                try
                {
                    CancellationTokenSource src = new CancellationTokenSource();
                    string password = Base64.encodeBytes(Util.getSecretBytes(18));
                    IdentityKeyPair tmpIdentity = KeyHelper.generateIdentityKeyPair();
                    SignalServiceAccountManager accountManager = new SignalServiceAccountManager(serviceUrls, src.Token, "Signal-Windows");

                    string uuid = accountManager.GetNewDeviceUuid(src.Token);
                    Debug.WriteLine("received uuid=" + uuid);
                    string tsdevice = "tsdevice:/?uuid=" + Uri.EscapeDataString(uuid) + "&pub_key=" + Uri.EscapeDataString(Base64.encodeBytesWithoutPadding(tmpIdentity.getPublicKey().serialize()));
                    Debug.WriteLine(tsdevice);

                    string tmpSignalingKey = Base64.encodeBytes(Util.getSecretBytes(52));
                    int registrationId = (int)KeyHelper.generateRegistrationId(false);
                    string deviceName = "windowstest";

                    NewDeviceLinkResult result = accountManager.FinishNewDeviceRegistration(tmpIdentity, tmpSignalingKey, password, false, true, registrationId, deviceName);
                    LocalSettings.Values["Username"] = result.Number;

                    new Manager(password, (uint) registrationId, result, tmpSignalingKey);
                    Debug.WriteLine("success!");
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                }
            });
        }
    }
}
