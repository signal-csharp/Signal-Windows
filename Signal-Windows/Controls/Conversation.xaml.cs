using libsignalservice.util;
using Signal_Windows.Lib;
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
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
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
        public VirtualizedCollection Collection;

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

        private bool _SendButtonEnabled;
        public bool SendButtonEnabled
        {
            get { return _SendButtonEnabled; }
            set
            {
                _SendButtonEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendButtonEnabled)));
            }
        }

        private Brush _SendButtonBackground;
        public Brush SendButtonBackground
        {
            get { return _SendButtonBackground; }
            set { _SendButtonBackground = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendButtonBackground))); }
        }

        private Symbol _SendButtonIcon;
        public Symbol SendButtonIcon
        {
            get { return _SendButtonIcon; }
            set { _SendButtonIcon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendButtonIcon))); }
        }

        public Conversation()
        {
            this.InitializeComponent();
            Displayname.Foreground = Utils.ForegroundIncoming;
            Separator.Foreground = Utils.ForegroundIncoming;
            Username.Foreground = Utils.ForegroundIncoming;
            SendButtonEnabled = true;
            SendButtonIcon = Symbol.Attach;
        }

        public MainPageViewModel GetMainPageVm()
        {
            return DataContext as MainPageViewModel;
        }

        private void UpdateHeader(SignalConversation thread)
        {
            ThreadDisplayName = thread.ThreadDisplayName;
            ThreadUsername = thread.ThreadId;
            if (thread is SignalContact contact)
            {
                HeaderBackground = contact.Color != null ? Utils.GetBrushFromColor((contact.Color)) :
                    Utils.GetBrushFromColor(Utils.CalculateDefaultColor(contact.ThreadDisplayName));
                SendButtonBackground = HeaderBackground;
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
                SendButtonBackground = HeaderBackground;
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
            Collection =  new VirtualizedCollection(conversation, this);
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

        public void Append(SignalMessageContainer sm)
        {
            var sourcePanel = (ItemsStackPanel)ConversationItemsControl.ItemsPanelRoot;
            bool bottom = sourcePanel.LastVisibleIndex == Collection.Count - 2; // -2 because we already incremented Count
            Collection.Add(sm, sm.Message.Author == null);
            if (bottom)
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
                    await GetMainPageVm().SendMessageButton_Click((TextBox)sender);
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
            if (string.IsNullOrEmpty(t.Text))
            {
                SendButtonIcon = Symbol.Attach;
            }
            else
            {
                SendButtonIcon = Symbol.Send;
            }
            SendButtonEnabled = true;
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
                App.CurrentSignalWindowsFrontend(ApplicationView.GetForCurrentView().Id).Locator.ConversationSettingsPageInstance.Contact = (SignalContact)SignalConversation;
                GetMainPageVm().View.Frame.Navigate(typeof(ConversationSettingsPage));
            }
        }

        private async void DeleteContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignalConversation is SignalContact)
            {
                GetMainPageVm().UnselectConversation();
                SignalDBContext.DeleteContact((SignalContact)SignalConversation);
                await SignalLibHandle.Instance.RefreshConversationList();
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
