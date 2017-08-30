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
        private bool SendingMessage = false;
        private Dictionary<long, SignalMessageContainer> OutgoingCache = new Dictionary<long, SignalMessageContainer>();
        //private SignalUnreadMarker UnreadMarker = new SignalUnreadMarker();
        private SignalConversation SignalConversation;
        //private bool UnreadMarkerAdded = false;
        private VirtualizedCollection Collection;

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

        private bool _SendEnabled;
        public bool SendEnabled
        {
            get { return _SendEnabled; }
            set
            {
                _SendEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendEnabled)));
            }
        }

        public Conversation()
        {
            this.InitializeComponent();
            Displayname.Foreground = Utils.ForegroundIncoming;
            Separator.Foreground = Utils.ForegroundIncoming;
            Username.Foreground = Utils.ForegroundIncoming;
            SendEnabled = false;
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

        public void Load(SignalConversation conversation)
        {
            SignalConversation = conversation;
            //UnreadMarkerAdded = false;
            InputTextBox.IsEnabled = false;
            DisposeCurrentThread();
            UpdateHeader(conversation);

            /*
             * When selecting a small (~650 messages) conversation after a bigger (~1800 messages) one,
             * ScrollToBottom would scroll so far south that the entire screen was white. Seems like the
             * scrollbar is not properly notified that the collection changed. I tried things like throwing
             * CollectionChanged (reset) event, but to no avail. This hack works, though.
             */
            ConversationItemsControl.ItemsSource = new List<object>();
            UpdateLayout();
            Collection =  new VirtualizedCollection(conversation);
            ConversationItemsControl.ItemsSource = Collection;
            UpdateLayout();
            InputTextBox.IsEnabled = conversation.CanReceive;
            ScrollToBottom();
        }

        public void DisposeCurrentThread()
        {
            OutgoingCache.Clear();
        }

        public T FindElementByName<T>(FrameworkElement element, string sChildName) where T : FrameworkElement
        {
            T childElement = null;
            var nChildCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < nChildCount; i++)
            {
                FrameworkElement child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;

                if (child == null)
                    continue;

                if (child is T && child.Name.Equals(sChildName))
                {
                    childElement = (T)child;
                    break;
                }

                childElement = FindElementByName<T>(child, sChildName);

                if (childElement != null)
                    break;
            }
            return childElement;
        }

        public void UpdateMessageBox(SignalMessage updatedMessage)
        {
            if (OutgoingCache.ContainsKey(updatedMessage.Id))
            {
                var m = OutgoingCache[updatedMessage.Id];
                var item = (ListBoxItem) ConversationItemsControl.ContainerFromIndex(m.Index);
                var message = FindElementByName<Message>(item, "ListBoxItemContent");
                bool retain = message.HandleUpdate(updatedMessage);
                if (!retain)
                {
                    OutgoingCache.Remove(m.Index);
                }
            }
        }

        public void Append(SignalMessageContainer sm, bool forceScroll)
        {
            Collection.Add(sm);
            if (forceScroll || true) //TODO
            {
                ScrollToBottom();
            }
        }

        public void AddToOutgoingMessagesCache(SignalMessageContainer m)
        {
            OutgoingCache[m.Message.Id] = m;
        }
        
        private async void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // this fixes double send by enter repeat
                if (!SendingMessage)
                {
                    SendingMessage = true;
                    await GetMainPageVm().TextBox_KeyDown(sender, e);
                    SendingMessage = false;
                }
            }
        }

        private void ScrollToBottom()
        {
            var lastMsg = ConversationItemsControl.Items[ConversationItemsControl.Items.Count - 1] as SignalMessageContainer;
            Debug.WriteLine($"scroll to {lastMsg}");
            ConversationItemsControl.ScrollIntoView(lastMsg);
        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            await GetMainPageVm().SendMessageButton_Click(InputTextBox);
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox t = sender as TextBox;
            SendEnabled = t.Text != string.Empty;
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
            if (item is SignalMessageContainer)
            {
                SignalMessage sm = ((SignalMessageContainer)item).Message;
                if (sm.Type == SignalMessageType.IdentityKeyChange)
                {
                    return IdentityKeyChangeMessage;
                }
                return NormalMessage;
            }
            return null;
        }
    }
}