using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using Signal_Windows.Lib;

namespace Signal_Windows.ViewModels
{
    public class ChatsAndMediaSettingsPageViewModel : ViewModelBase
    {
        private bool spellCheck;
        public bool SpellCheck
        {
            get { return spellCheck; }
            set { spellCheck = value; RaisePropertyChanged(nameof(SpellCheck)); }
        }

        private bool sendMessageWithEnter;
        public bool SendMessageWithEnter
        {
            get { return sendMessageWithEnter; }
            set { sendMessageWithEnter = value; RaisePropertyChanged(nameof(SendMessageWithEnter)); }
        }

        public void OnNavigatedTo()
        {
            SpellCheck = GlobalSettingsManager.SpellCheckSetting;
            SendMessageWithEnter = GlobalSettingsManager.SendMessageWithEnterSetting;
        }

        public void SpellCheckToggleSwitch_Toggled(bool value)
        {
            GlobalSettingsManager.SpellCheckSetting = value;
        }

        public void SendMessageWithEnterToggleSwitch_Toggled(bool value)
        {
            GlobalSettingsManager.SendMessageWithEnterSetting = value;
        }
    }
}
