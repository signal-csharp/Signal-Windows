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
                Background = GetSolidColorBrush(255, "#f3f3f3");
                Foreground = GetSolidColorBrush(255, "#454545");
                HorizontalAlignment = HorizontalAlignment.Right;
                TimestampTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                Foreground = GetSolidColorBrush(255, "#ffffff");
                Background = GetSolidColorBrush(255, Model.Author.Color);
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

        public SolidColorBrush GetSolidColorBrush(byte opacity, string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte r = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(opacity, r, g, b));
            return myBrush;
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