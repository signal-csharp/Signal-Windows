using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Signal_Windows.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AttachmentDetailsPage : Page
    {
        public ObservableCollection<string> Attachments { get; set; } = new ObservableCollection<string>();

        public AttachmentDetailsPage()
        {
            this.InitializeComponent();
        }

        public string Path { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Utils.EnableBackButton(BackButton_Click);
            Attachments.Clear();
            Attachments.Add($"{ApplicationData.Current.LocalCacheFolder.Path}/Attachments/{(e.Parameter as SignalAttachment).Id}.plain");
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(BackButton_Click);
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            Frame.GoBack();
            e.Handled = true;
        }
    }
}
