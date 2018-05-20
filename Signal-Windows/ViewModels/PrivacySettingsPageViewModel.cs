using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using Signal_Windows.Lib;
using Windows.UI.ViewManagement;

namespace Signal_Windows.ViewModels
{
    public class PrivacySettingsPageViewModel : ViewModelBase
    {
        private bool blockScreenshots;
        public bool BlockScreenshots
        {
            get { return blockScreenshots; }
            set { blockScreenshots = value; RaisePropertyChanged(nameof(BlockScreenshots)); }
        }

        private bool readReceipts;
        public bool ReadReceipts
        {
            get { return readReceipts; }
            set { readReceipts = value; RaisePropertyChanged(nameof(ReadReceipts)); }
        }

        public void OnNavigatedTo()
        {
            BlockScreenshots = GlobalSettingsManager.BlockScreenshotsSetting;
            ReadReceipts = GlobalSettingsManager.EnableReadReceiptsSetting;
        }

        public void BlockScreenshotsToggleSwitch_Toggled(bool value)
        {
            GlobalSettingsManager.BlockScreenshotsSetting = value;
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = !GlobalSettingsManager.BlockScreenshotsSetting;
        }

        public void ReadReceiptsToggleSwitch_Toggled(bool value)
        {
            GlobalSettingsManager.EnableReadReceiptsSetting = value;
        }
    }
}
