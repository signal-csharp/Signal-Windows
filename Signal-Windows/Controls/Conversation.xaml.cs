using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class Conversation : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Dictionary<long, Message> OutgoingCache = new Dictionary<long, Message>();

        public RangeObservableCollection<object> Messages { get; set; } = new RangeObservableCollection<object>();
        private SignalUnreadMarker UnreadMarker = new SignalUnreadMarker();
        private bool UnreadMarkerAdded = false;

        private string _ThreadDisplayName;

        public string ThreadDisplayName
        {
            get { return _ThreadDisplayName; }
            set { _ThreadDisplayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThreadDisplayName))); }
        }

        private string _ThreadUsername;

        public string ThreadUsername
        {
            get { return _ThreadUsername; }
            set { _ThreadUsername = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThreadUsername))); }
        }

        private Visibility _ThreadUsernameVisibility;

        public Visibility ThreadUsernameVisibility
        {
            get { return _ThreadUsernameVisibility; }
            set { _ThreadUsernameVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThreadUsernameVisibility))); }
        }

        private Visibility _SeparatorVisiblity;

        public Visibility SeparatorVisibility
        {
            get { return _SeparatorVisiblity; }
            set { _SeparatorVisiblity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeparatorVisibility))); }
        }

        private Brush _HeaderBackground;

        public Brush HeaderBackground
        {
            get { return _HeaderBackground; }
            set { _HeaderBackground = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderBackground))); }
        }

        public Conversation()
        {
            this.InitializeComponent();
            Displayname.Foreground = Utils.ForegroundIncoming;
            Separator.Foreground = Utils.ForegroundIncoming;
            Username.Foreground = Utils.ForegroundIncoming;
        }

        public MainPageViewModel GetMainPageVm()
        {
            return DataContext as MainPageViewModel;
        }

        public void Update(SignalConversation thread)
        {
            InputTextBox.IsEnabled = thread.CanReceive;
            UpdateHeader(thread);
        }

        private void UpdateHeader(SignalConversation thread)
        {
            ThreadDisplayName = thread.ThreadDisplayName;
            ThreadUsername = thread.ThreadId;
            if (thread is SignalContact)
            {
                SignalContact contact = (SignalContact)thread;
                HeaderBackground = Utils.GetBrushFromColor(contact.Color);
                if (ThreadUsername != ThreadDisplayName)
                {
                    ThreadUsernameVisibility = Visibility.Visible;
                    SeparatorVisibility = Visibility.Visible;
                }
                else
                {
                    ThreadUsernameVisibility = Visibility.Collapsed;
                    SeparatorVisibility = Visibility.Collapsed;
                }
            }
            else
            {
                HeaderBackground = Utils.Blue;
                ThreadUsernameVisibility = Visibility.Collapsed;
                SeparatorVisibility = Visibility.Collapsed;
            }
        }

        public void ScrollToBottom()
        {
            SelectedMessagesScrollViewer.UpdateLayout();
            if (UnreadMarkerAdded)
            {
                var transform = UnreadMarker.View.TransformToVisual((UIElement)SelectedMessagesScrollViewer.Content);
                var position = transform.TransformPoint(new Point(0, 0));
                SelectedMessagesScrollViewer.ChangeView(null, position.Y, null, true);
            }
            else
            {
                SelectedMessagesScrollViewer.ChangeView(null, double.MaxValue, null, true);
            }
        }

        public async Task Load(SignalConversation thread)
        {
            UnreadMarkerAdded = false;
            InputTextBox.IsEnabled = false;
            DisposeCurrentThread();
            UpdateHeader(thread);
            var before = Util.CurrentTimeMillis();
            var messages = await Task.Run(() =>
            {
                return SignalDBContext.GetMessagesLocked(thread);
            });
            var after1 = Util.CurrentTimeMillis();
            foreach (var message in messages)
            {
                Messages.AddSilently(message);
                if (thread.LastSeenMessageId == message.Id && thread.LastMessageId != message.Id)
                {
                    UnreadMarkerAdded = true;
                    Messages.AddSilently(UnreadMarker);
                }
            }
            Messages.ForceCollectionChanged();
            UpdateLayout();
            if (UnreadMarkerAdded)
            {
                UnreadMarker.View.SetText(thread.UnreadCount + " UNREAD MESSAGE" + (thread.UnreadCount > 1 ? "S" : ""));
            }
            foreach (var message in messages)
            {
                if (message.Direction != SignalMessageDirection.Incoming)
                {
                    AddToOutgoingMessagesCache(message);
                }
            }
            var after2 = Util.CurrentTimeMillis();
            Debug.WriteLine("db query: " + (after1 - before));
            Debug.WriteLine("ui: " + (after2 - after1));
            InputTextBox.IsEnabled = thread.CanReceive;
        }

        public void DisposeCurrentThread()
        {
            Messages.Clear();
            OutgoingCache.Clear();
        }

        public void UpdateMessageBox(SignalMessage updatedMessage)
        {
            if (OutgoingCache.ContainsKey(updatedMessage.Id))
            {
                var m = OutgoingCache[updatedMessage.Id];
                m.UpdateMessageBox(updatedMessage);
            }
        }

        public void Append(SignalMessage sm)
        {
            Messages.Add(sm);
            //TODO move scrolltobottom here
        }

        public void AddToOutgoingMessagesCache(SignalMessage sm)
        {
            if (sm.View != null)
            {
                OutgoingCache[sm.Id] = sm.View;
            }
            else
            {
                throw new Exception("Attempt to add null view to OutgoingCache");
            }
        }

        private async void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            await GetMainPageVm().TextBox_KeyDown(sender, e);
        }

        public void RemoveUnreadMarker()
        {
            if (UnreadMarkerAdded)
            {
                Messages.Remove(UnreadMarker);
                UnreadMarkerAdded = false;
            }
        }
    }

    public class SignalUnreadMarker
    {
        public UnreadMarker View { get; set; }
    }

    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalMessage { get; set; }
        public DataTemplate UnreadMarker { get; set; }
        public DataTemplate IdentityKeyChangeMessage { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;
            if (item is SignalMessage)
            {
                SignalMessage sm = (SignalMessage)item;
                if (sm.Type == SignalMessageType.IdentityKeyChange)
                {
                    return IdentityKeyChangeMessage;
                }
                return NormalMessage;
            }
            if (item is SignalUnreadMarker)
            {
                return UnreadMarker;
            }
            return null;
        }
    }
}