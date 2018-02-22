using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using ByteSizeLib;
using libsignalservice.messages;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class Attachment : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string fileName;
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName))); }
        }

        private string fileSize;
        public string FileSize
        {
            get { return fileSize; }
            set { fileSize = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize))); }
        }

        private Symbol attachmentIcon;
        public Symbol AttachmentIcon
        {
            get { return attachmentIcon; }
            set { attachmentIcon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachmentIcon))); }
        }

        private bool canDownload;
        public bool CanDownload
        {
            get { return canDownload; }
            set { canDownload = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanDownload))); }
        }

        public SignalAttachment Model
        {
            get { return DataContext as SignalAttachment; }
            set { DataContext = value; }
        }

        public Attachment()
        {
            this.InitializeComponent();
            DataContextChanged += Attachment_DataContextChanged;
        }

        private void Attachment_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                if (string.IsNullOrEmpty(Model.FileName))
                {
                    FileName = Model.SentFileName;
                }
                else
                {
                    FileName = Model.FileName;
                }
                FileSize = ByteSize.FromBytes(Model.Size).ToString("0.");
                if (Model.Status == SignalAttachmentStatus.Default || Model.Status == SignalAttachmentStatus.Finished ||
                    Model.Status == SignalAttachmentStatus.Failed)
                {
                    AttachmentIcon = Symbol.Page2;
                }
                else if (Model.Status == SignalAttachmentStatus.InProgress)
                {
                    AttachmentIcon = Symbol.Download;
                }
                else if (Model.Status == SignalAttachmentStatus.Failed_Permanently)
                {
                    AttachmentIcon = Symbol.Cancel;
                }
                
                if (Model.Status != SignalAttachmentStatus.Failed_Permanently)
                {
                    CanDownload = true;
                }
                else
                {
                    CanDownload = false;
                }
            }
        }

        private async void AttachmentDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model != null && Model.Status != SignalAttachmentStatus.Failed_Permanently)
            {
                SignalServiceAttachmentPointer attachmentPointer = Model.ToAttachmentPointer();
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile tempFile = await localFolder.CreateFileAsync(Model.StorageId.ToString(), CreationCollisionOption.GenerateUniqueName);
                string displayName = Model.SentFileName;
                if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(Model.FileName))
                {
                    displayName = Model.FileName;
                }
                BackgroundDownloader downloader = new BackgroundDownloader();
                downloader.SetRequestHeader("Content-Type", "application/octet-stream");
                downloader.SuccessToastNotification = LibUtils.CreateToastNotification($"{displayName} has finished downloading.");
                downloader.FailureToastNotification = LibUtils.CreateToastNotification($"{displayName} has failed to download.");
                // this is the recommended way to call CreateDownload
                // see https://docs.microsoft.com/en-us/uwp/api/windows.networking.backgroundtransfer.backgrounddownloader#Methods
                DownloadOperation download = await Task.Run(() =>
                {
                    return downloader.CreateDownload(new Uri(SignalLibHandle.Instance.RetrieveAttachmentDownloadUrl(attachmentPointer)), tempFile);
                });
                Model.Guid = download.Guid.ToString();
                Model.Status = SignalAttachmentStatus.InProgress;
                SignalDBContext.UpdateAttachmentLocked(Model);
                Task downloadTask = SignalLibHandle.Instance.HandleDownload(download, true, Model);
            }
        }
    }
}
