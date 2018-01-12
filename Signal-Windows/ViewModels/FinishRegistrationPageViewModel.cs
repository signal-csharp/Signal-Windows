using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.util;
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
                    string SignalingKey = Base64.encodeBytes(Util.getSecretBytes(52));
                    ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.AccountManager.verifyAccountWithCode(
                        ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.VerificationCode,
                            SignalingKey, ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.SignalRegistrationId,
                            false, false, true);
                    SignalStore store = new SignalStore()
                    {
                        DeviceId = 1,
                        IdentityKeyPair = Base64.encodeBytes(ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.IdentityKeyPair.serialize()),
                        NextSignedPreKeyId = 1,
                        Password = ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.Password,
                        PreKeyIdOffset = 1,
                        Registered = true,
                        RegistrationId = ViewModelLocator.CurrentVML.RegisterFinalizationPageInstance.SignalRegistrationId,
                        SignalingKey = SignalingKey,
                        Username = ViewModelLocator.CurrentVML.RegisterPageInstance.FinalNumber,
                    };
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Store = store;
                    }).AsTask().Wait();

                    /* create prekeys */
                    LibsignalDBContext.RefreshPreKeys(
                        new SignalServiceAccountManager(App.ServiceUrls, store.Username, store.Password, (int)store.DeviceId, App.USER_AGENT));

                    /* reload again with prekeys and their offsets */
                    store = LibsignalDBContext.GetSignalStore();
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
    }
}
