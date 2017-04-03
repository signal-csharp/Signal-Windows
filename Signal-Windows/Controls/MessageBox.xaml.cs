using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class MessageBox : UserControl
    {
        public bool IsExtended { get; set; }
        public string FancyTimestamp { get; set; }
        public SolidColorBrush TextColor { get; set; }
        public SolidColorBrush TimestampColor { get; set; }

        public MessageBox()
        {
            this.InitializeComponent();
            this.DataContextChanged += MessageBox_DataContextChanged;
        }

        private void MessageBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            //name opacity 204
            if (Model.Author == null)
            {
                Background = Utils.Outgoing;
                TextColor = Utils.GetSolidColorBrush(255, "#454545");
                TimestampColor = Utils.GetSolidColorBrush(127, "#454545");
                HorizontalAlignment = HorizontalAlignment.Right;
                TimestampTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                Background = Utils.GetBrushFromColor(Model.Author.Color);
                TextColor = Utils.GetSolidColorBrush(255, "#ffffff");
                TimestampColor = Utils.GetSolidColorBrush(127, "#ffffff");
                HorizontalAlignment = HorizontalAlignment.Left;
                TimestampTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
            }
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(Model.ReceivedTimestamp / 1000);
            DateTime dt = dateTimeOffset.UtcDateTime.ToLocalTime();
            FancyTimestamp = dt.ToString();
        }

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
    }
}