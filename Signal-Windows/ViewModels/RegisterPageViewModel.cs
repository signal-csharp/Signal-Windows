using GalaSoft.MvvmLight;
using Signal_Windows.Views;
using System.Threading;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class RegisterPageViewModel : ViewModelBase
    {
        private ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        public CancellationTokenSource CancelSource = new CancellationTokenSource();
        public string PhoneNumber { get; set; }
        public string ConfirmationCode { get; set; }
        public RegisterPage View { get; internal set; }

        internal void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            LocalSettings.Values["Username"] = PhoneNumber;
            LocalSettings.Values["DeviceId"] = 1;
            SignalManager = new Manager(CancelSource.Token, PhoneNumber, false);
            SignalManager.Register(false);
            */
        }

        internal void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (SignalManager != null)
            {
                string code = ConfirmationCode.Replace("-", "");
                SignalManager.VerifyAccount(code); //TODO handle errors
                LocalSettings.Values["Active"] = true;
                View.NavigateForward();
            }
            */
        }
    }
}