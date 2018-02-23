using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Signal_Windows.Models;
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

        private Uri imagePath;
        public Uri ImagePath
        {
            get { return imagePath; }
            set { imagePath = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImagePath))); }
        }

        public SignalAttachment Model
        {
            get { return DataContext as SignalAttachment; }
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
                FileName = Model.FileName;
                FileSize = Utils.BytesToString(Model.Size);
                if (Model.Status == SignalAttachmentStatus.Finished)
                {
                    if (IMAGE_TYPES.Contains(Model.ContentType))
                    {
                        var path = ApplicationData.Current.LocalCacheFolder.Path + @"\Attachments\" + Model.Id + ".plain";
                        ImagePath = new Uri(path);
                    }
                }
            }
        }

        private static HashSet<string> IMAGE_TYPES = new HashSet<string>()
        {
            "image/jpeg",
            "image/png",
            "image/gif"
        };
    }
}
