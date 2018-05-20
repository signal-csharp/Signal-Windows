using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;

namespace Signal_Windows.ViewModels
{
    public class NotificationSettingsPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<NotificationSettingsPageViewModel>();

        public string NameAndMessageTag { get { return "NameAndMessage"; } }
        public string NameOnlyTag { get { return "NameOnly"; } }
        public string NoNameOrMessageTag { get { return "NoNameOrMessage"; } }

        private bool nameAndMessageChecked;
        public bool NameAndMessageChecked
        {
            get { return nameAndMessageChecked; }
            set { nameAndMessageChecked = value; RaisePropertyChanged(nameof(NameAndMessageChecked)); }
        }

        private bool nameOnlyChecked;
        public bool NameOnlyChecked
        {
            get { return nameOnlyChecked; }
            set { nameOnlyChecked = value; RaisePropertyChanged(nameof(NameOnlyChecked)); }
        }

        private bool noNameOrMessageChecked;
        public bool NoNameOrMessageChecked
        {
            get { return noNameOrMessageChecked; }
            set { noNameOrMessageChecked = value; RaisePropertyChanged(nameof(NoNameOrMessageChecked)); }
        }

        public void OnNavigatedTo()
        {
            var showNotificationTextSettings = GlobalSettingsManager.ShowNotificationTextSetting;
            if (showNotificationTextSettings == GlobalSettingsManager.ShowNotificationTextSettings.NameAndMessage)
            {
                NameAndMessageChecked = true;
            }
            else if (showNotificationTextSettings == GlobalSettingsManager.ShowNotificationTextSettings.NameOnly)
            {
                NameOnlyChecked = true;
            }
            else if (showNotificationTextSettings == GlobalSettingsManager.ShowNotificationTextSettings.NoNameOrMessage)
            {
                NoNameOrMessageChecked = true;
            }
        }
    }
}
