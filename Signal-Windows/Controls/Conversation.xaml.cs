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
    public interface IMessageView
    {
        SignalMessage Model { get; set; }
        void HandleUpdate(SignalMessage m);
        FrameworkElement AsFrameworkElement();
    }

    public sealed partial class Conversation : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<Conversation>();
        public event PropertyChangedEventHandler PropertyChanged;
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
            Loaded += Conversation_Loaded;
            Unloaded += Conversation_Unloaded;
        }

        private void Conversation_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated -= HandleWindowActivated;
        }

        private void Conversation_Loaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated += HandleWindowActivated;
        }

        private void HandleWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            Logger.LogTrace("HandleWindowActivated() new activation state {0}", e.WindowActivationState);
            ActivationState = e.WindowActivationState;
            MarkBottommostMessageRead();
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
            else
            {
                // Need to make sure to reset the Blocked and SendMessageVisible values in case
                // a group chat is selected. Group chats can never be blocked.
                Blocked = false;
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
            Collection =  new VirtualizedCollection(conversation);
            ConversationItemsControl.ItemsSource = Collection;
            UpdateLayout();
            InputTextBox.IsEnabled = conversation.CanReceive;
            ScrollToUnread();
        }

        public void DisposeCurrentThread()
        {
            Collection?.Dispose();
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
            if (Collection != null)
            {
                IMessageView m = Collection.GetMessageByDbId(updatedMessage.Id);
                if (m != null)
                {
                    var attachment = FindElementByName<Attachment>(m.AsFrameworkElement(), "Attachment");
                    m.HandleUpdate(updatedMessage);
                }
            }
        }

        public void UpdateAttachment(SignalAttachment sa)
        {
            if (Collection != null)
            {
                IMessageView m = Collection.GetMessageByDbId(sa.Message.Id);
                if (m != null)
                {
                    var attachment = FindElementByName<Attachment>(m.AsFrameworkElement(), "Attachment");
                    attachment.HandleUpdate(sa);
                }
            }
        }

        public AppendResult Append(Message sm)
        {
            AppendResult result = null;
            bool bottom = GetBottommostIndex() == Collection.Count - 2; // -2 because we already incremented Count
            Collection.Add(sm, sm.Model.Author == null);
            if (bottom)
            {
                UpdateLayout();
                ScrollToBottom();
                if (ActivationState != CoreWindowActivationState.Deactivated)
                {
                    result = new AppendResult(GetBottommostIndex()); //TODO correct?
                }
            }
            return result;
        }
        
        private async void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true; // Prevent KeyDown from firing twice on W10 CU
                bool shift = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (shift)
                {
                    InputTextBox.Text += "\r";
                    InputTextBox.SelectionStart = InputTextBox.Text.Length;
                    InputTextBox.SelectionLength = 0;
                }
                else
                {
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
            if (SignalConversation is SignalContact)
            {
                App.CurrentSignalWindowsFrontend(ApplicationView.GetForCurrentView().Id).Locator.ConversationSettingsPageInstance.Contact = (SignalContact)SignalConversation;
                GetMainPageVm().View.Frame.Navigate(typeof(ConversationSettingsPage));
            }
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ActivationState != CoreWindowActivationState.Deactivated)
            {
                MarkBottommostMessageRead();
            }
        }

        private void MarkBottommostMessageRead()
        {
            if (Collection != null)
            {
                int bottomIndex = GetBottommostIndex();
                int rawBottomIndex = Collection.GetRawIndex(bottomIndex);
                long lastSeenIndex = SignalConversation.LastSeenMessageIndex;
                if (lastSeenIndex <= rawBottomIndex && LastMarkReadRequest < rawBottomIndex)
                {
                    LastMarkReadRequest = rawBottomIndex;
                    var msg = ((IMessageView)Collection[bottomIndex]).Model;
                    Task.Run(async () =>
                    {
                        await App.Handle.SetMessageRead(rawBottomIndex, msg, SignalConversation);
                    });
                }
            }
        }

        private async void UnblockButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignalConversation is SignalContact contact)
            {
                contact.Blocked = false;
                Blocked = false;
                SendMessageVisible = !Blocked;
                SignalDBContext.UpdateBlockStatus(contact);
                await Task.Run(() =>
                {
                    App.Handle.SendBlockedMessage();
                });
            }
        }
    }
}