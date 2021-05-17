using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class Attachment : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<Attachment>();
        public event PropertyChangedEventHandler PropertyChanged;

        private Uri imagePath;
        public Uri ImagePath
        {
            get { return imagePath; }
            set { imagePath = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImagePath))); }
        }

        private Symbol attachmentIcon;
        public Symbol AttachmentIcon
        {
            get { return attachmentIcon; }
            set { attachmentIcon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachmentIcon))); }
        }

        public SignalAttachment Model
        {
            get { return DataContext as SignalAttachment; }
        }

        public Attachment()
        {
            this.InitializeComponent();
            DataContextChanged += Attachment_DataContextChanged;
            AttachmentImage.ImageFailed += AttachmentImage_ImageFailed;
        }

        private void AttachmentImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Logger.LogError("AttachmentImage_ImageFailed {0}", e.ErrorMessage);
        }

        private void Attachment_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                if (Model.Status == SignalAttachmentStatus.Finished || Model.Message.Direction == SignalMessageDirection.Outgoing)
                {
                    AttachmentImage.Visibility = Visibility.Visible;
                    AttachmentDownloadIcon.Visibility = Visibility.Collapsed;
                    if (IMAGE_TYPES.Contains(Model.ContentType))
                    {
                        AttachmentSaveIcon.Visibility = Visibility.Collapsed;
                        var path = ApplicationData.Current.LocalCacheFolder.Path + @"\Attachments\" + Model.Id + ".plain";
                        ImagePath = new Uri(path);
                    }
                    else
                    {
                        AttachmentSaveIcon.Visibility = Visibility.Visible;
                    }
                }
                else if (Model.Status == SignalAttachmentStatus.Default || Model.Status == SignalAttachmentStatus.Finished || Model.Status == SignalAttachmentStatus.Failed)
                {
                    AttachmentImage.Visibility = Visibility.Collapsed;
                    AttachmentDownloadIcon.Visibility = Visibility.Visible;
                    AttachmentSaveIcon.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void AttachmentDownloadIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            App.Handle.StartAttachmentDownload(Model);
        }
        private async void AttachmentSaveIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await App.Handle.ExportAttachment(Model);
        }

        private void AttachmentImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (IsDetailsPageEnabled)
            {
                App.CurrentSignalWindowsFrontend(ApplicationView.GetForCurrentView().Id).Locator.MainPageInstance.OpenAttachment(Model);
            }
        }

        public bool HandleUpdate(SignalAttachment sa)
        {
            DataContext = sa;
            return Model.Status != SignalAttachmentStatus.Finished && Model.Status != SignalAttachmentStatus.Failed_Permanently;
        }

        private bool IsDetailsPageEnabled => IMAGE_TYPES.Contains(Model.ContentType);

        private static HashSet<string> IMAGE_TYPES = new HashSet<string>()
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/bmp"
        };
    }
}
