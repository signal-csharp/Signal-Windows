using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.ViewModels;
using Signal_Windows.Views;
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
        private SignalConversation SignalConversation;
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
            ScrollToUnread();
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
                var item = (ListViewItem) ConversationItemsControl.ContainerFromIndex(Collection.GetVirtualIndex(m.Index));
                if (item != null)
                {
                    var message = FindElementByName<Message>(item, "ListBoxItemContent");
                    bool retain = message.HandleUpdate(updatedMessage);
                    if (!retain)
                    {
                        OutgoingCache.Remove(m.Index);
                    }
                }
            }
        }

        public void Append(SignalMessageContainer sm, bool forceScroll)
        {
            var sourcePanel = (ItemsStackPanel)ConversationItemsControl.ItemsPanelRoot;
            bool bottom = sourcePanel.LastVisibleIndex == Collection.Count - 2; /* -2 because we already incremented Count */
            Collection.Add(sm, true);
            if (forceScroll || bottom)
            {
                UpdateLayout();
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
            if (Collection.Count > 0)
            {
                var lastUnreadMsg = ConversationItemsControl.Items[Collection.Count - 1];
                ConversationItemsControl.ScrollIntoView(lastUnreadMsg, ScrollIntoViewAlignment.Leading);
            }
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

        private void ScrollToUnread()
        {
            if (Collection.Count > 0)
            {
                if (Collection.UnreadMarkerIndex > 0)
                {
                    var lastUnreadMsg = ConversationItemsControl.Items[Collection.UnreadMarkerIndex];
                    ConversationItemsControl.ScrollIntoView(lastUnreadMsg, ScrollIntoViewAlignment.Leading);
                }
                else
                {
                    var lastUnreadMsg = ConversationItemsControl.Items[Collection.Count - 1];
                    ConversationItemsControl.ScrollIntoView(lastUnreadMsg, ScrollIntoViewAlignment.Leading);
                }
            }
        }

        private void ConversationSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignalConversation is SignalContact)
            {
                App.ViewModels.ConversationSettingsPageInstance.Contact = (SignalContact)SignalConversation;
                GetMainPageVm().View.Frame.Navigate(typeof(ConversationSettingsPage));
            }
        }
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
                SignalMessageContainer smc = (SignalMessageContainer)item;
                SignalMessage sm = smc.Message;
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