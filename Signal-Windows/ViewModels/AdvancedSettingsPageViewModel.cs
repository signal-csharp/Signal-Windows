using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Storage;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Signal_Windows.ViewModels
{
    public class AdvancedSettingsPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<AdvancedSettingsPageViewModel>();

        public async Task ExportUIDebugLog()
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "Signal-Windows.ui.log";
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                await Task.Run(() =>
                {
                    SignalFileLoggerProvider.ExportUILog(file);
                });
            }
            else
            {
                Logger.LogTrace("No file was selected");
            }
        }
    }
}
