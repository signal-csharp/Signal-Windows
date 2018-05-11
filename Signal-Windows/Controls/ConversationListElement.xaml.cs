using Signal_Windows.Models;
using System.ComponentModel;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadStringVisibility)));
            }
        }

        public Visibility UnreadStringVisibility
        {
            get
            {
                if (UnreadCount > 0)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
            set
            {
                // we never set this
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
            set
            {
                // we never set this
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

        private Brush _FillBrush = Utils.Blue;
        public Brush FillBrush
        {
            get
            {
                return _FillBrush;
            }
            set
            {
                _FillBrush = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillBrush)));
            }
        }

        private string _Initials = string.Empty;
        public string Initials
        {
            get
            {
                return _Initials;
            }
            set
            {
                _Initials = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Initials)));
            }
        }

        private string _LastMessageTimestamp = string.Empty;
        public string LastMessageTimestamp
        {
            get { return _LastMessageTimestamp; }
            set { _LastMessageTimestamp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastMessageTimestamp))); }
        }

        private void ThreadListItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                UpdateConversationDisplay();
                Model.UpdateUI = UpdateConversationDisplay;
            }
        }

        public void UpdateConversationDisplay()
        {
            if (Model != null)
            {
                ConversationDisplayName.Text = Model.ThreadDisplayName;
                UnreadCount = Model.UnreadCount;
                LastMessage = Model.LastMessage?.Content.Content;
                Initials = Utils.GetInitials(Model.ThreadDisplayName);
                FillBrush = Model is SignalContact contact ?
                    contact.Color != null ? Utils.GetBrushFromColor((contact.Color)) :
                        Utils.GetBrushFromColor(Utils.CalculateDefaultColor(Model.ThreadDisplayName)) : Utils.Blue;
                LastMessageTimestamp = Utils.GetTimestamp(Model.LastActiveTimestamp);
            }
        }
    }
}