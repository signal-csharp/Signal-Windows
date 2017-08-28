using Signal_Windows.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class ConversationListElement : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ConversationListElement()
        {
            this.InitializeComponent();
            this.DataContextChanged += ThreadListItem_DataContextChanged;
        }

        public SignalConversation Model
        {
            get
            {
                return DataContext as SignalConversation;
            }
            set
            {
                DataContext = value;
            }
        }

        private uint _UnreadCount;

        public uint UnreadCount
        {
            get
            {
                return _UnreadCount;
            }
            set
            {
                _UnreadCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadString)));
            }
        }

        public string UnreadString
        {
            get
            {
                if (UnreadCount != 0)
                {
                    return UnreadCount.ToString();
                }
                else
                {
                    return "";
                }
            }
        }

        private string _LastMessage = "@";

        public string LastMessage
        {
            get
            {
                return _LastMessage;
            }
            set
            {
                _LastMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastMessage)));
            }
        }

        private void ThreadListItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                Model.UpdateUI += Model_UpdateUI;
                ConversationDisplayName.Text = Model.ThreadDisplayName;
                UnreadCount = Model.UnreadCount;
                LastMessage = Model.LastMessage?.Content.Content;
            }
            else
            {
                Debug.WriteLine("ConversationListElement datacontext changed to null");
            }
        }

        private void Model_UpdateUI(object sender, EventArgs e)
        {
            var conversation = (SignalConversation)sender;
            if (Model != null && Model == conversation)
            {
                ConversationDisplayName.Text = Model.ThreadDisplayName;
                UnreadCount = Model.UnreadCount;
                LastMessage = Model.LastMessage?.Content.Content;
            }
            else
            {
                Debug.WriteLine("Model_UpdateUI: stale update event received");
                conversation.UpdateUI -= Model_UpdateUI;
            }
        }
    }
}