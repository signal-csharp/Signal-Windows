using GalaSoft.MvvmLight;
using libsignal;
using libsignal.util;
using libsignalservice;
using libsignalservice.messages.multidevice;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Signal_Windows.ViewModels
{
    public class DeviceSettingsPageViewmodel : ViewModelBase
    {
        private static readonly ILogger Logger = LibsignalLogging.CreateLogger<AdvancedSettingsPageViewModel>();

        public ReadOnlyObservableCollection<DeviceViewmodel> Devices { get; }
        private readonly ObservableCollection<DeviceViewmodel> devices;

        public DeviceSettingsPageViewmodel()
        {
            this.devices = new ObservableCollection<DeviceViewmodel>();
            this.Devices = new ReadOnlyObservableCollection<DeviceViewmodel>(this.devices);
        }

        public async void OnNavigatedTo()
        {
            await this.RefreshList();
        }

        private async Task RefreshList()
        {
            this.devices.Clear();
            var devices = await App.Handle.AccountManager.GetDevicesAsync();
            this.devices.AddRange(devices.Select(x => new DeviceViewmodel(x)));
        }

        public async Task AddDevice(Uri uri)
        {
            var toAdd = DeviceProtocoll.FromUri(uri);

            var code = await App.Handle.AccountManager.GetNewDeviceVerificationCodeAsync();

            // I'm not sure where which parameter goes...
            // but it failes when the QR code is to old, so the first two are propably correct...
            await App.Handle.AccountManager.AddDeviceAsync(toAdd.Uuid, toAdd.IdentetyKey.getPublicKey(),
                     new IdentityKeyPair(Base64.Decode(App.Handle.Store.IdentityKeyPair)),
                     Base64.Decode(App.Handle.Store.SignalingKey),
                     code);


            await this.RefreshList();
        }

    }

    public class DeviceProtocoll
    {
        private DeviceProtocoll(string uuid, IdentityKey identetyKey)
        {
            this.Uuid = uuid;
            this.IdentetyKey = identetyKey;
        }

        public string Uuid { get; }
        public IdentityKey IdentetyKey { get; }

        public static DeviceProtocoll FromUri(Uri uri)
        {
            if (uri.Scheme.ToLower() != "tsdevice")
                throw new ArgumentException(nameof(uri), $"The protocoll must be tsdevice but was {uri.Scheme}");
            if (uri.Query.Length <= 1)
                throw new ArgumentException(nameof(uri), $"The querry is not valid. Quary was: {uri.Query}");

            var query = uri.Query.Substring(1);

            var parameters = query.Split('&');
            string uuid = null;
            string pub_key = null;

            foreach (var item in parameters)
            {
                if (item.StartsWith("uuid="))
                    uuid = item.Substring("uuid=".Length);
                else if (item.StartsWith("pub_key="))
                    pub_key = item.Substring("pub_key=".Length);
            }

            if (pub_key == null || uuid == null)
            {
                throw new ArgumentException(nameof(uri),
                    (pub_key == null
                    ? "The pub_key parameter is missing."
                    : "")
                    + (uuid == null
                    ? "The uuid parameter is missing."
                    : "")
                                        );

            }

            var publicKeyBytes = Base64.Decode(Uri.UnescapeDataString(pub_key));
            var identetyKey = new IdentityKey(publicKeyBytes, 0);
            return new DeviceProtocoll(uuid, identetyKey);
        }
    }


    public class DeviceViewmodel : ViewModelBase
    {

        public DeviceViewmodel(DeviceInfo device)
        {
            this.Name = device.Name;
        }

        public string Name { get; }
    }
}
