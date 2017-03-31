using GalaSoft.MvvmLight;
using Signal_Windows.Models;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class ThreadViewModel : ViewModelBase
    {
        public MainPageViewModel MainPageVm;
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();

        private string _ThreadTitle;

        public string ThreadTitle
        {
            get
            {
                return _ThreadTitle;
            }
            set
            {
                _ThreadTitle = value;
                RaisePropertyChanged("ThreadTitle");
            }
        }

        private Visibility _WelcomeVisibility;

        public Visibility WelcomeVisibility
        {
            get
            {
                return _WelcomeVisibility;
            }
            set
            {
                _WelcomeVisibility = value;
                RaisePropertyChanged("WelcomeVisibility");
            }
        }

        private Visibility _MainVisibility;

        public Visibility MainVisibility
        {
            get
            {
                return _MainVisibility;
            }
            set
            {
                _MainVisibility = value;
                RaisePropertyChanged("MainVisibility");
            }
        }

        public ThreadViewModel(MainPageViewModel mainPageVm)
        {
            this.MainPageVm = mainPageVm;
        }
    }
}