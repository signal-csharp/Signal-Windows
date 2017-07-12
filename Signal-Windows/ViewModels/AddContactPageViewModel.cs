using GalaSoft.MvvmLight;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class AddContactPageViewModel : ViewModelBase
    {
        public MainPageViewModel MainPageVM;

        private string _ContactName = "";

        public string ContactName
        {
            get { return _ContactName; }
            set { _ContactName = value; RaisePropertyChanged("ContactName"); }
        }

        private string _ContactNumber = "";

        public string ContactNumber
        {
            get { return _ContactNumber; }
            set { _ContactNumber = value; RaisePropertyChanged("ContactNumber"); }
        }

        private bool _UIEnabled = true;

        public bool UIEnabled
        {
            get { return _UIEnabled; }
            set { _UIEnabled = value; RaisePropertyChanged("UIEnabled"); }
        }

        internal async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            UIEnabled = false;
            Debug.WriteLine("creating contact {0} ({1})", ContactName, ContactNumber);
            SignalContact contact = new SignalContact()
            {
                ThreadDisplayName = ContactName,
                ThreadId = ContactNumber,
                CanReceive = true,
                AvatarFile = null,
                LastActiveTimestamp = 0,
                LastMessage = null,
                Color = "red",
                Unread = 0
            };
            ContactName = "";
            ContactNumber = "";
            await Task.Run(() =>
            {
                SignalDBContext.AddOrUpdateContactLocked(contact, MainPageVM);
            });
            UIEnabled = true;
        }
    }
}
