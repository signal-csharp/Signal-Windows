﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

        private bool attachmentDownloaded;
        public bool AttachmentDownloaded
        {
            get { return attachmentDownloaded; }
            set { attachmentDownloaded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachmentDownloaded))); }
        }

        public Attachment()
        {
            this.InitializeComponent();
            DataContextChanged += Attachment_DataContextChanged;
        }

        private async void Attachment_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                if (Model.Status == SignalAttachmentStatus.Finished)
                {
                    AttachmentDownloaded = true;
                    if (IMAGE_TYPES.Contains(Model.ContentType))
                    {
                        try
                        {
                            string fileExtension = LibUtils.GetAttachmentExtension(Model);
                            // using ms-appdata is the only way to get the image properties
                            StorageFile imageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($@"ms-appdata:///LocalCache\Attachments\{Model.Id}.{fileExtension}"));
                            var properties = await imageFile.Properties.GetImagePropertiesAsync();
                            ImagePath = new Uri(imageFile.Path);
                        }
                        catch (FileNotFoundException)
                        {
                        }
                    }
                }
                else if (Model.Status == SignalAttachmentStatus.Default || Model.Status == SignalAttachmentStatus.Finished || Model.Status == SignalAttachmentStatus.Failed)
                {
                    AttachmentDownloaded = false;
                }
            }
        }

        public static Visibility Negate(bool value)
        {
            if (value)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        private static HashSet<string> IMAGE_TYPES = new HashSet<string>()
        {
            "image/jpeg",
            "image/png",
            "image/gif"
        };

        private void AttachmentDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            App.Handle.StartAttachmentDownload(Model);
        }
    }
}
