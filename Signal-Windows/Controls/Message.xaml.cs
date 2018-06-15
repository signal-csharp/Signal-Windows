using Signal_Windows.Lib;
using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class Message : UserControl, INotifyPropertyChanged
    {
        // This is taken from https://gist.github.com/gruber/8891611
        // This is public domain: https://daringfireball.net/2010/07/improved_regex_for_matching_urls
        private const string UrlRegexString = @"(?i)\b((?:https?:(?:/{1,3}|[a-z0-9%])|[a-z0-9.\-]+[.](?:com|net|org|edu|gov|mil|aero|asia|biz|cat|coop|info|int|jobs|mobi|museum|name|post|pro|tel|travel|xxx|ac|ad|ae|af|ag|ai|al|am|an|ao|aq|ar|as|at|au|aw|ax|az|ba|bb|bd|be|bf|bg|bh|bi|bj|bm|bn|bo|br|bs|bt|bv|bw|by|bz|ca|cc|cd|cf|cg|ch|ci|ck|cl|cm|cn|co|cr|cs|cu|cv|cx|cy|cz|dd|de|dj|dk|dm|do|dz|ec|ee|eg|eh|er|es|et|eu|fi|fj|fk|fm|fo|fr|ga|gb|gd|ge|gf|gg|gh|gi|gl|gm|gn|gp|gq|gr|gs|gt|gu|gw|gy|hk|hm|hn|hr|ht|hu|id|ie|il|im|in|io|iq|ir|is|it|je|jm|jo|jp|ke|kg|kh|ki|km|kn|kp|kr|kw|ky|kz|la|lb|lc|li|lk|lr|ls|lt|lu|lv|ly|ma|mc|md|me|mg|mh|mk|ml|mm|mn|mo|mp|mq|mr|ms|mt|mu|mv|mw|mx|my|mz|na|nc|ne|nf|ng|ni|nl|no|np|nr|nu|nz|om|pa|pe|pf|pg|ph|pk|pl|pm|pn|pr|ps|pt|pw|py|qa|re|ro|rs|ru|rw|sa|sb|sc|sd|se|sg|sh|si|sj|Ja|sk|sl|sm|sn|so|sr|ss|st|su|sv|sx|sy|sz|tc|td|tf|tg|th|tj|tk|tl|tm|tn|to|tp|tr|tt|tv|tw|tz|ua|ug|uk|us|uy|uz|va|vc|ve|vg|vi|vn|vu|wf|ws|ye|yt|yu|za|zm|zw)/)(?:[^\s()<>{}\[\]]+|\([^\s()]*?\([^\s()]+\)[^\s()]*?\)|\([^\s]+?\))+(?:\([^\s()]*?\([^\s()]+\)[^\s()]*?\)|\([^\s]+?\)|[^\s`!()\[\]{};:'"".,<>?«»“”‘’])|(?:(?<!@)[a-z0-9]+(?:[.\-][a-z0-9]+)*[.](?:com|net|org|edu|gov|mil|aero|asia|biz|cat|coop|info|int|jobs|mobi|museum|name|post|pro|tel|travel|xxx|ac|ad|ae|af|ag|ai|al|am|an|ao|aq|ar|as|at|au|aw|ax|az|ba|bb|bd|be|bf|bg|bh|bi|bj|bm|bn|bo|br|bs|bt|bv|bw|by|bz|ca|cc|cd|cf|cg|ch|ci|ck|cl|cm|cn|co|cr|cs|cu|cv|cx|cy|cz|dd|de|dj|dk|dm|do|dz|ec|ee|eg|eh|er|es|et|eu|fi|fj|fk|fm|fo|fr|ga|gb|gd|ge|gf|gg|gh|gi|gl|gm|gn|gp|gq|gr|gs|gt|gu|gw|gy|hk|hm|hn|hr|ht|hu|id|ie|il|im|in|io|iq|ir|is|it|je|jm|jo|jp|ke|kg|kh|ki|km|kn|kp|kr|kw|ky|kz|la|lb|lc|li|lk|lr|ls|lt|lu|lv|ly|ma|mc|md|me|mg|mh|mk|ml|mm|mn|mo|mp|mq|mr|ms|mt|mu|mv|mw|mx|my|mz|na|nc|ne|nf|ng|ni|nl|no|np|nr|nu|nz|om|pa|pe|pf|pg|ph|pk|pl|pm|pn|pr|ps|pt|pw|py|qa|re|ro|rs|ru|rw|sa|sb|sc|sd|se|sg|sh|si|sj|Ja|sk|sl|sm|sn|so|sr|ss|st|su|sv|sx|sy|sz|tc|td|tf|tg|th|tj|tk|tl|tm|tn|to|tp|tr|tt|tv|tw|tz|ua|ug|uk|us|uy|uz|va|vc|ve|vg|vi|vn|vu|wf|ws|ye|yt|yu|za|zm|zw)\b/?(?!@)))";
        private static Regex urlRegex = new Regex(UrlRegexString);

        public event PropertyChangedEventHandler PropertyChanged;

        public SignalMessage Model
        {
            get
            {
                return DataContext as SignalMessage;
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
                UpdateMessageTextBlock();
                if (Model.Author == null)
                {
                    MessageAuthor.Visibility = Visibility.Collapsed;
                    MessageBoxBorder.Background = Utils.BackgroundOutgoing;
                    MessageContentTextBlock.Foreground = Utils.ForegroundOutgoing;
                    FancyTimestampBlock.Foreground = Utils.GetSolidColorBrush(127, "#454545");
                    HorizontalAlignment = HorizontalAlignment.Right;
                    FooterPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    if (Model.Status == SignalMessageStatus.Pending)
                    {
                        CheckImage.Visibility = Visibility.Collapsed;
                        DoubleCheckImage.Visibility = Visibility.Collapsed;
                        ResendTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else if (Model.Status == SignalMessageStatus.Confirmed)
                    {
                        CheckImage.Visibility = Visibility.Visible;
                        DoubleCheckImage.Visibility = Visibility.Collapsed;
                        ResendTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else if (Model.Status == SignalMessageStatus.Received)
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
                    if (Model.ThreadId.EndsWith("="))
                    {
                        MessageAuthor.Visibility = Visibility.Visible;
                        MessageAuthor.Text = Model.Author.ThreadDisplayName;
                    }
                    else
                    {
                        MessageAuthor.Visibility = Visibility.Collapsed;
                    }
                    MessageBoxBorder.Background = Model.Author.Color != null ? Utils.GetBrushFromColor(Model.Author.Color) : Utils.GetBrushFromColor(Utils.CalculateDefaultColor(Model.Author.ThreadDisplayName));
                    MessageAuthor.Foreground = Utils.GetSolidColorBrush(204, "#ffffff");
                    MessageContentTextBlock.Foreground = Utils.ForegroundIncoming;
                    FancyTimestampBlock.Foreground = Utils.GetSolidColorBrush(127, "#ffffff");
                    HorizontalAlignment = HorizontalAlignment.Left;
                    FooterPanel.HorizontalAlignment = HorizontalAlignment.Left;
                }
                FancyTimestampBlock.Text = Utils.GetTimestamp(Model.ComposedTimestamp);

                HasAttachment = false;
                if (Model.Attachments?.Count > 0)
                {
                    HasAttachment = true;
                    Attachment = Model.Attachments[0];
                }
            }
        }

        private void UpdateMessageTextBlock()
        {
            string messageText = Model.Content.Content;
            var matches = urlRegex.Matches(messageText);
            if (matches.Count == 0)
            {
                MessageContentTextBlock.Text = messageText;
            }
            else
            {
                MessageContentTextBlock.Inlines.Clear();
                int previousIndex = 0;
                int currentIndex = 0;
                foreach (Match match in matches)
                {
                    // First create a Run of the text before the link
                    currentIndex = match.Index;
                    var length = currentIndex - previousIndex;
                    if (length > 0)
                    {
                        Run run = new Run
                        {
                            Text = messageText.Substring(previousIndex, currentIndex - previousIndex)
                        };
                        MessageContentTextBlock.Inlines.Add(run);
                    }

                    // Now add the hyperlink
                    string link = match.Value;
                    Hyperlink hyperlink = new Hyperlink();
                    Run hyperlinkRun = new Run
                    {
                        Text = link
                    };
                    try
                    {
                        hyperlink.NavigateUri = new Uri(link);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    hyperlink.UnderlineStyle = UnderlineStyle.Single;
                    hyperlink.Inlines.Add(hyperlinkRun);
                    MessageContentTextBlock.Inlines.Add(hyperlink);
                    previousIndex = currentIndex + match.Length;
                    currentIndex = previousIndex;
                }

                // Then finish up by adding the rest of the message text to the TextBox
                var restLength = messageText.Length - currentIndex;
                if (restLength > 0)
                {
                    Run restRun = new Run
                    {
                        Text = messageText.Substring(currentIndex, restLength)
                    };
                    MessageContentTextBlock.Inlines.Add(restRun);
                }
            }
        }

        private void MessageBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            UpdateUI();
        }

        private void ResendTextBlock_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            App.Handle.ResendMessage(Model);
        }

        internal bool HandleUpdate(SignalMessage updatedMessage)
        {
            Model.Status = updatedMessage.Status;
            UpdateUI();
            return updatedMessage.Status != SignalMessageStatus.Received;
        }
    }
}
