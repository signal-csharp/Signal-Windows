using Signal_Windows.Models;
using System.ComponentModel;
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UnreadString"));
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

        private void ThreadListItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                Model.View = this;
                ConversationDisplayName.Text = Model.ThreadDisplayName;
                UnreadCount = Model.UnreadCount;
            }
        }

        public void Update(SignalConversation thread)
        {
            Model.ThreadDisplayName = thread.ThreadDisplayName;
            Model.LastActiveTimestamp = thread.LastActiveTimestamp;
            ConversationDisplayName.Text = thread.ThreadDisplayName;
            UnreadCount = thread.UnreadCount;
        }
    }
}