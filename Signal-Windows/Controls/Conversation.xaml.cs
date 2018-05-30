using libsignalservice;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Lib.Models;
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
using Windows.System;
using Windows.UI.Core;
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
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<Conversation>();
        public event PropertyChangedEventHandler PropertyChanged;
        private Dictionary<long, SignalMessageContainer> OutgoingCache = new Dictionary<long, SignalMessageContainer>();
        private Dictionary<long, SignalAttachmentContainer> UnfinishedAttachmentsCache = new Dictionary<long, SignalAttachmentContainer>();
        private SignalConversation SignalConversation;
        public VirtualizedCollection Collection;
        private CoreWindowActivationState ActivationState = CoreWindowActivationState.Deactivated;
        private int LastMarkReadRequest;

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

        public Brush SendButtonBackground
        {
            get { return Utils.Blue; }
        }

        private bool blocked;
        public bool Blocked
        {
            get { return blocked; }
            set
            {
                blocked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blocked)));
            }
        }

        public bool SendMessageVisible
        {
            get { return !Blocked; }
            set { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendMessageVisible))); }
        }

        public Conversation()
        {
            this.InitializeComponent();
            Displayname.Foreground = Utils.ForegroundIncoming;
            Separator.Foreground = Utils.ForegroundIncoming;
            Username.Foreground = Utils.ForegroundIncoming;
            SendButtonEnabled = false;
            Window.Current.Activated += HandleWindowActivated;
        }

        private void HandleWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            Logger.LogTrace("HandleWindowActivated() new activation state {0}", e.WindowActivationState);
            ActivationState = e.WindowActivationState;
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
            if (SignalConversation is SignalContact contact)
            {
                Blocked = contact.Blocked;
                SendMessageVisible = !Blocked;
            }
            LastMarkReadRequest = -1;
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
            UnfinishedAttachmentsCache.Clear();
        }

        public T FindElementByName<T>(FrameworkElement element, string sChildName) where T : FrameworkElement
        {
            T childElement = null;
            var nChildCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < nChildCount; i++)
            {
                if (!(VisualTreeHelper.GetChild(element, i) is FrameworkElement child))
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

        public void UpdateAttachment(SignalAttachment sa)
        {
            if (UnfinishedAttachmentsCache.ContainsKey(sa.Id))
            {
                var a = UnfinishedAttachmentsCache[sa.Id];
                var messageItem = (ListViewItem)ConversationItemsControl.ContainerFromIndex(Collection.GetVirtualIndex(a.MessageIndex));
                if (messageItem != null)
                {
                    var message = FindElementByName<Message>(messageItem, "ListBoxItemContent");
                    var attachment = FindElementByName<Attachment>(message, "Attachment");
                    bool retain = attachment.HandleUpdate(sa);
                    if (!retain)
                    {
                        OutgoingCache.Remove(sa.Id);
                    }
                }
            }
        }

        public AppendResult Append(SignalMessageContainer sm)
        {
            AppendResult result = null;
            bool bottom = GetBottommostIndex() == Collection.Count - 2; // -2 because we already incremented Count
            Collection.Add(sm, sm.Message.Author == null);
            if (bottom)
            {
                UpdateLayout();
                ScrollToBottom();
                if (ActivationState != CoreWindowActivationState.Deactivated)
                {
                    result = new AppendResult(sm.Index);
                }
            }
            return result;
        }

        public void AddToOutgoingMessagesCache(SignalMessageContainer m)
        {
            OutgoingCache[m.Message.Id] = m;
        }

        public void AddToUnfinishedAttachmentsCache(SignalAttachmentContainer m)
        {
            UnfinishedAttachmentsCache[m.Attachment.Id] = m;
        }
        
        private async void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                bool shift = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (!shift)
                {
                    e.Handled = true;
                    bool sendMessageResult = await GetMainPageVm().SendMessage(InputTextBox.Text);
                    if (sendMessageResult)
                    {
                        InputTextBox.Text = string.Empty;
                    }
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
            InputTextBox.Focus(FocusState.Programmatic);
            bool sendMessageResult = await GetMainPageVm().SendMessage(InputTextBox.Text);
            if (sendMessageResult)
            {
                InputTextBox.Text = string.Empty;
            }
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox t = sender as TextBox;
            SendButtonEnabled = t.Text != string.Empty;
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

        private int GetBottommostIndex()
        {
            if (ConversationItemsControl.ItemsPanelRoot is ItemsStackPanel sourcePanel)
            {
                return sourcePanel.LastVisibleIndex;
            }
            else
            {
                Logger.LogError("GetBottommostIndex() ItemsPanelRoot is not a valid ItemsStackPanel ({0})", ConversationItemsControl.ItemsPanelRoot);
                return -1;
            }
        }

        private void ConversationSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignalConversation is SignalContact contact)
            {
                App.CurrentSignalWindowsFrontend(ApplicationView.GetForCurrentView().Id).Locator.ConversationSettingsPageInstance.Contact = contact;
                GetMainPageVm().View.Frame.Navigate(typeof(ConversationSettingsPage));
            }
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ActivationState != CoreWindowActivationState.Deactivated)
            {
                int bottomIndex = GetBottommostIndex();
                int rawBottomIndex = Collection.GetRawIndex(bottomIndex);
                long lastSeenIndex = SignalConversation.LastSeenMessageIndex;
                if (lastSeenIndex <= rawBottomIndex && LastMarkReadRequest < rawBottomIndex)
                {
                    LastMarkReadRequest = rawBottomIndex;
                    Task.Run(async () =>
                    {
                        await App.Handle.SetMessageRead(rawBottomIndex, ((SignalMessageContainer) Collection[bottomIndex]).Message, SignalConversation);
                    });
                }
            }
        }

        private void UnblockButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignalConversation is SignalContact contact)
            {
                contact.Blocked = false;
                Blocked = false;
                SendMessageVisible = !Blocked;
                SignalDBContext.UpdateBlockStatus(contact);
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
            if (item is SignalMessageContainer smc)
            {
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