using GalaSoft.MvvmLight;
using Signal_Windows.Signal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Signal_UWP.Views;

namespace Signal_UWP.ViewModels
{
    public class RegisterPageViewModel : ViewModelBase
    {
        ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        public CancellationTokenSource CancelSource = new CancellationTokenSource();
        public Manager SignalManager;
        public string PhoneNumber { get; set; }
        public string ConfirmationCode { get; set; }
        public RegisterPage View { get; internal set; }

        internal void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            LocalSettings.Values["Username"] = PhoneNumber;
            LocalSettings.Values["DeviceId"] = 1;
            SignalManager = new Manager(CancelSource.Token, PhoneNumber, false);
            SignalManager.Register(false);
        }

        internal void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if(SignalManager != null)
            {
                string code = ConfirmationCode.Replace("-", "");
                SignalManager.VerifyAccount(code); //TODO handle errors
                LocalSettings.Values["Active"] = true;
                View.NavigateForward();
            }
        }
    }
}
