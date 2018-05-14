using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.util;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using Windows.UI.Popups;

namespace Signal_Windows.ViewModels
{
    public class FinishRegistrationPageViewModel : ViewModelBase
    {
        public FinishRegistrationPage View { get; set; }

        internal async Task OnNavigatedTo()
        {
            try
            {
                await Task.Run(() =>
                {
                    string SignalingKey = Base64.EncodeBytes(Util.getSecretBytes(52));
                    App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.AccountManager.VerifyAccountWithCode(
                        App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.VerificationCode.Replace("-", ""),
                            SignalingKey, App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.SignalRegistrationId,
                            true);
                    SignalStore store = new SignalStore()
                    {
                        DeviceId = 1,
                        IdentityKeyPair = Base64.EncodeBytes(App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.IdentityKeyPair.serialize()),
                        NextSignedPreKeyId = 1,
                        Password = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.Password,
                        PreKeyIdOffset = 1,
                        Registered = true,
                        RegistrationId = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.SignalRegistrationId,
                        SignalingKey = SignalingKey,
                        Username = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.FinalNumber,
                    };
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Handle.Store = store;
                    }).AsTask().Wait();

                    /* create prekeys */
                    LibsignalDBContext.RefreshPreKeys(
                        new SignalServiceAccountManager(App.ServiceConfiguration, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));

                    /* reload again with prekeys and their offsets */
                    store = LibsignalDBContext.GetSignalStore();
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Handle.Store = store;
                    }).AsTask().Wait();
                });
                var frontend = App.CurrentSignalWindowsFrontend(App.MainViewId);
                await App.Handle.Reacquire();
                View.Frame.Navigate(typeof(MainPage));
            }
            catch (Exception e)
            {
                // TODO log exception
                var title = "Verification failed";
                var content = "Please enter the correct verification code.";
                MessageDialog dialog = new MessageDialog(content, title);
                var result = dialog.ShowAsync();
                View.Frame.Navigate(typeof(RegisterPage));
            }
        }
    }
}
