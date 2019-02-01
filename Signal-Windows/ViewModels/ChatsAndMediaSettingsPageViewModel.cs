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

        public void OnNavigatedTo()
        {
            SpellCheck = GlobalSettingsManager.SpellCheckSetting;
        }

        public void SpellCheckToggleSwitch_Toggled(bool value)
        {
            GlobalSettingsManager.SpellCheckSetting = value;
        }
    }
}
