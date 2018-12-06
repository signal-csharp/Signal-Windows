using GalaSoft.MvvmLight;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI.Core;

namespace Signal_Windows.ViewModels
{
    public class GlobalSettingsPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<GlobalSettingsPageViewModel>();
        public GlobalSettingsPage View;

        public void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            View.Frame.GoBack();
            e.Handled = true;
        }

        public void OnNavigatedTo()
        {
        }
    }
}
