using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class Message : UserControl, INotifyPropertyChanged
    {
        public Visibility HeaderVisibility { get; set; }
        public string ContactName { get; set; }
        public string FancyTimestamp { get; set; }
        public SolidColorBrush ContactNameColor { get; set; }
        public SolidColorBrush TextColor { get; set; }
        public SolidColorBrush TimestampColor { get; set; }
        public Visibility ResendVisibility { get; set; } = Visibility.Collapsed;
        public Visibility CheckVisibility { get; set; } = Visibility.Collapsed;
        public Visibility DoubleCheckVisibility { get; set; } = Visibility.Collapsed;

        public SignalMessage Model
        {
            get
            {
                return this.DataContext as SignalMessage;
            }
            set
            {
                this.DataContext = value;
            }
        }

        public Message()
        {
            this.InitializeComponent();
            this.DataContextChanged += MessageBox_DataContextChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MessageBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Model.View = this;
            if (Model.Author == null)
            {
                HeaderVisibility = Visibility.Collapsed;
                Background = Utils.BackgroundOutgoing;
                TextColor = Utils.ForegroundOutgoing;
                TimestampColor = Utils.GetSolidColorBrush(127, "#454545");
                HorizontalAlignment = HorizontalAlignment.Right;
                FooterPanel.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                if (Model.ThreadId[0] != '+')
                {
                    HeaderVisibility = Visibility.Visible;
                    ContactName = Model.Author.ThreadDisplayName;
                }
                else
                {
                    HeaderVisibility = Visibility.Collapsed;
                }
                Background = Utils.GetBrushFromColor(Model.Author.Color);
                ContactNameColor = Utils.GetSolidColorBrush(204, "#ffffff");
                TextColor = Utils.ForegroundIncoming;
                TimestampColor = Utils.GetSolidColorBrush(127, "#ffffff");
                HorizontalAlignment = HorizontalAlignment.Left;
                FooterPanel.HorizontalAlignment = HorizontalAlignment.Left;
            }
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(Model.ComposedTimestamp / 1000);
            DateTime dt = dateTimeOffset.UtcDateTime.ToLocalTime();
            FancyTimestamp = dt.ToString();
            UpdateSignalMessageStatusIcon(Model);
            UpdateResendButton(Model);
        }

        private void UpdateSignalMessageStatusIcon(SignalMessage updatedMessage)
        {
            if (updatedMessage.Direction == SignalMessageDirection.Outgoing)
            {
                if (updatedMessage.Status == SignalMessageStatus.Pending)
                {
                    Model.Status = (uint)SignalMessageStatus.Pending;
                    CheckVisibility = Visibility.Collapsed;
                    DoubleCheckVisibility = Visibility.Collapsed;
                }
                else if (updatedMessage.Status == SignalMessageStatus.Confirmed)
                {
                    Model.Status = SignalMessageStatus.Confirmed;
                    CheckVisibility = Visibility.Visible;
                    DoubleCheckVisibility = Visibility.Collapsed;
                }
                else if (updatedMessage.Status == SignalMessageStatus.Received)
                {
                    Model.Status = SignalMessageStatus.Received;
                    CheckVisibility = Visibility.Collapsed;
                    DoubleCheckVisibility = Visibility.Visible;
                }
            }
            else if (updatedMessage.Direction == SignalMessageDirection.Synced)
            {
                if (updatedMessage.Receipts == 0)
                {
                    CheckVisibility = Visibility.Visible;
                    DoubleCheckVisibility = Visibility.Collapsed;
                }
                else
                {
                    CheckVisibility = Visibility.Collapsed;
                    DoubleCheckVisibility = Visibility.Visible;
                }
            }
        }

        private void UpdateResendButton(SignalMessage updatedMessage)
        {
            if (updatedMessage.Direction == SignalMessageDirection.Outgoing && updatedMessage.Status != SignalMessageStatus.Pending && updatedMessage.Status != SignalMessageStatus.Confirmed && updatedMessage.Status != SignalMessageStatus.Received)
            {
                ResendVisibility = Visibility.Visible;
            }
            else
            {
                ResendVisibility = Visibility.Collapsed;
            }
        }

        public void UpdateMessageBox(SignalMessage updatedMessage)
        {
            UpdateSignalMessageStatusIcon(updatedMessage);
            UpdateResendButton(updatedMessage);
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(CheckVisibility)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(DoubleCheckVisibility)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(ResendVisibility)));
        }

        private async void AttachmentSaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("", new List<string>() { "." });
                savePicker.SuggestedFileName = "test";
                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    var button = (Button)sender;
                    var attachment = (SignalAttachment)button.DataContext;
                    StorageFile src = await StorageFile.GetFileFromPathAsync(ApplicationData.Current.LocalFolder.Path + @"\Attachments\" + attachment.FileName);
                    await src.CopyAndReplaceAsync(file);
                }
                else
                {
                    Debug.WriteLine("no file picked");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void TextBlock_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {

        }

        private void ResendTextBlock_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            App.ViewModels.MainPageInstance.OutgoingQueue.Add(Model);
            //TODO prevent button smashing
        }
    }
}