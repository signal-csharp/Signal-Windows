using Signal_Windows.Lib;
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
        public event PropertyChangedEventHandler PropertyChanged;

        public SignalMessageContainer Model
        {
            get
            {
                return DataContext as SignalMessageContainer;
            }
            set
            {
                this.DataContext = value;
            }
        }

        private bool hasAttachment;
        public bool HasAttachment
        {
            get { return hasAttachment; }
            set { hasAttachment = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAttachment))); }
        }

        private SignalAttachment attachment;
        public SignalAttachment Attachment
        {
            get { return attachment; }
            set { attachment = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Attachment))); }
        }

        public Message()
        {
            this.InitializeComponent();
            this.DataContextChanged += MessageBox_DataContextChanged;
        }

        private void UpdateUI()
        {
            if (Model != null)
            {
                MessageContentTextBlock.Text = Model.Message.Content.Content;
                if (Model.Message.Author == null)
                {
                    MessageAuthor.Visibility = Visibility.Collapsed;
                    MessageBoxBorder.Background = Utils.BackgroundOutgoing;
                    MessageContentTextBlock.Foreground = Utils.ForegroundOutgoing;
                    FancyTimestampBlock.Foreground = Utils.GetSolidColorBrush(127, "#454545");
                    HorizontalAlignment = HorizontalAlignment.Right;
                    FooterPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    if (Model.Message.Status == SignalMessageStatus.Pending)
                    {
                        CheckImage.Visibility = Visibility.Collapsed;
                        DoubleCheckImage.Visibility = Visibility.Collapsed;
                        ResendTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else if (Model.Message.Status == SignalMessageStatus.Confirmed)
                    {
                        CheckImage.Visibility = Visibility.Visible;
                        DoubleCheckImage.Visibility = Visibility.Collapsed;
                        ResendTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else if (Model.Message.Status == SignalMessageStatus.Received)
                    {
                        CheckImage.Visibility = Visibility.Collapsed;
                        DoubleCheckImage.Visibility = Visibility.Visible;
                        ResendTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CheckImage.Visibility = Visibility.Collapsed;
                        DoubleCheckImage.Visibility = Visibility.Collapsed;
                        ResendTextBlock.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    CheckImage.Visibility = Visibility.Collapsed;
                    DoubleCheckImage.Visibility = Visibility.Collapsed;
                    ResendTextBlock.Visibility = Visibility.Collapsed;
                    if (Model.Message.ThreadId.EndsWith("="))
                    {
                        MessageAuthor.Visibility = Visibility.Visible;
                        MessageAuthor.Text = Model.Message.Author.ThreadDisplayName;
                    }
                    else
                    {
                        MessageAuthor.Visibility = Visibility.Collapsed;
                    }
                    MessageBoxBorder.Background = Model.Message.Author.Color != null ? Utils.GetBrushFromColor(Model.Message.Author.Color) : Utils.GetBrushFromColor(Utils.CalculateDefaultColor(Model.Message.Author.ThreadDisplayName));
                    MessageAuthor.Foreground = Utils.GetSolidColorBrush(204, "#ffffff");
                    MessageContentTextBlock.Foreground = Utils.ForegroundIncoming;
                    FancyTimestampBlock.Foreground = Utils.GetSolidColorBrush(127, "#ffffff");
                    HorizontalAlignment = HorizontalAlignment.Left;
                    FooterPanel.HorizontalAlignment = HorizontalAlignment.Left;
                }
                FancyTimestampBlock.Text = Utils.GetTimestamp(Model.Message.ComposedTimestamp);

                HasAttachment = false;
                if (Model.Message.Attachments?.Count > 0)
                {
                    HasAttachment = true;
                    Attachment = Model.Message.Attachments[0];
                }
            }
        }

        private void MessageBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            UpdateUI();
        }

        private void ResendTextBlock_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            App.Handle.ResendMessage(Model.Message);
        }

        internal bool HandleUpdate(SignalMessage updatedMessage)
        {
            Model.Message.Status = updatedMessage.Status;
            UpdateUI();
            return updatedMessage.Status != SignalMessageStatus.Received;
        }
    }
}