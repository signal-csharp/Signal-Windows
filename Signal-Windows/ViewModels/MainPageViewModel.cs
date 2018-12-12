using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.messages;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Signal_Windows.Controls;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<MainPageViewModel>();
        public string RequestedConversationId;
        public SignalConversation SelectedThread;
        private Dictionary<string, SignalConversation> ConversationsDictionary = new Dictionary<string, SignalConversation>();
        public MainPage View;
        public ObservableCollection<SignalConversation> Conversations { get; set; } = new ObservableCollection<SignalConversation>();

        private Visibility _ThreadVisibility = Visibility.Collapsed;
        public Visibility ThreadVisibility
        {
            get { return _ThreadVisibility; }
            set { _ThreadVisibility = value; RaisePropertyChanged(nameof(ThreadVisibility)); }
        }

        private Visibility _WelcomeVisibility;
        public Visibility WelcomeVisibility
        {
            get { return _WelcomeVisibility; }
            set { _WelcomeVisibility = value; RaisePropertyChanged(nameof(WelcomeVisibility)); }
        }

        private SignalConversation _SelectedConversation = null;
        public SignalConversation SelectedConversation
        {
            get { return _SelectedConversation; }
            set
            {
                if (_SelectedConversation != value)
                {
                    _SelectedConversation = value;
                    RaisePropertyChanged(nameof(SelectedConversation));
                }
            }
        }

        private bool _IsPaneOpen = false;
        public bool IsPaneOpen
        {
            get => _IsPaneOpen;
            set
            {
                if (_IsPaneOpen != value)
                {
                    _IsPaneOpen = value;
                    RaisePropertyChanged(nameof(IsPaneOpen));
                }
            }
        }

        private double _CompactPaneLength = 0;
        public double CompactPaneLength
        {
            get => _CompactPaneLength;
            set
            {
                if (_CompactPaneLength != value)
                {
                    _CompactPaneLength = value;
                    RaisePropertyChanged(nameof(CompactPaneLength));
                }
            }
        }

        private double _OpenPaneLength = 320;
        public double OpenPaneLength
        {
            get => _OpenPaneLength;
            set
            {
                if (_OpenPaneLength != value)
                {
                    _OpenPaneLength = value;
                    RaisePropertyChanged(nameof(OpenPaneLength));
                }
            }
        }

        private SplitViewDisplayMode _DisplayMode = SplitViewDisplayMode.CompactInline;
        public SplitViewDisplayMode DisplayMode
        {
            get => _DisplayMode;
            set
            {
                if (_DisplayMode != value)
                {
                    _DisplayMode = value;
                    RaisePropertyChanged(nameof(DisplayMode));
                }
            }
        }

        internal void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            SelectedThread = null;
            View.Thread.DisposeCurrentThread();
            ThreadVisibility = Visibility.Collapsed;
            WelcomeVisibility = Visibility.Visible;
            View.SwitchToStyle(View.GetCurrentViewStyle());
            Utils.DisableBackButton(BackButton_Click);
            e.Handled = true;
        }

        private bool _ThreadListAlignRight;

        public bool ThreadListAlignRight
        {
            get { return _ThreadListAlignRight; }
            set { _ThreadListAlignRight = value; RaisePropertyChanged(nameof(ThreadListAlignRight)); }
        }

        internal async Task<bool> SendMessage(string messageText, StorageFile attachment)
        {
            try
            {
                if (messageText != string.Empty || attachment != null)
                {
                    messageText = messageText.Replace("\r", "\r\n");
                    await App.Handle.SendMessage(messageText, attachment, SelectedThread);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
                return false;
            }
        }

        internal void RepositionConversation(SignalConversation uiConversation)
        {
            for (int i = 0; i < Conversations.Count; i++)
            {
                var c = Conversations[i];
                if (c == uiConversation)
                {
                    break;
                }
                if (uiConversation.LastActiveTimestamp > c.LastActiveTimestamp)
                {
                    var oldSelectedConversation = SelectedConversation;
                    int index = Conversations.IndexOf(uiConversation);
                    Logger.LogDebug("RepositionConversation() moving conversation from {0} to {1}", index, i);
                    Conversations.Move(index, i);
                    if (oldSelectedConversation == uiConversation)
                    {
                        SelectedConversation = uiConversation;
                    }
                    break;
                }
            }
        }

        internal void Deselect()
        {
            if (SelectedConversation != null)
            {
                RequestedConversationId = SelectedConversation.ThreadId;
            }
            SelectedConversation = null;
            SelectedThread = null;
        }

        public void TrySelectConversation(string conversationId)
        {
            if (conversationId != null && ConversationsDictionary.ContainsKey(conversationId))
            {
                SelectedThread = null;
                SelectedConversation = ConversationsDictionary[conversationId];
            }
        }

        public void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                var conversation = e.AddedItems[0];
                if (conversation != SelectedThread)
                {
                    Logger.LogDebug("ContactsList_SelectionChanged()");
                    WelcomeVisibility = Visibility.Collapsed;
                    ThreadVisibility = Visibility.Visible;
                    SelectedThread = SelectedConversation;
                    View.Thread.Load(SelectedThread);
                    View.SwitchToStyle(View.GetCurrentViewStyle());
                }
            }
        }

        #region SignalFrontend API
        public void OpenAttachment(SignalAttachment sa)
        {
            View.Frame.Navigate(typeof(AttachmentDetailsPage), sa);
        }

        public void AddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage)
        {
            SignalConversation uiConversation;
            if (!ConversationsDictionary.ContainsKey(conversation.ThreadId))
            {
                uiConversation = conversation.Clone();
                Conversations.Add(uiConversation);
                ConversationsDictionary.Add(uiConversation.ThreadId, uiConversation);
            }
            else
            {
                uiConversation = ConversationsDictionary[conversation.ThreadId];
                uiConversation.LastActiveTimestamp = conversation.LastActiveTimestamp;
                uiConversation.CanReceive = conversation.CanReceive;
                uiConversation.ExpiresInSeconds = conversation.ExpiresInSeconds;
                uiConversation.LastMessage = conversation.LastMessage;
                uiConversation.LastSeenMessage = conversation.LastSeenMessage;
                uiConversation.LastSeenMessageIndex = conversation.LastSeenMessageIndex;
                uiConversation.MessagesCount = conversation.MessagesCount;
                uiConversation.ThreadDisplayName = conversation.ThreadDisplayName;
                uiConversation.UnreadCount = conversation.UnreadCount;
                if (uiConversation is SignalContact ourContact && conversation is SignalContact newContact)
                {
                    ourContact.Color = newContact.Color;
                }
                else if (uiConversation is SignalGroup ourGroup && conversation is SignalGroup newGroup)
                {
                    ourGroup.GroupMemberships = newGroup.GroupMemberships;
                }
                if (SelectedThread != null) // the conversation we have open may have been edited
                {
                    if (SelectedThread == uiConversation)
                    {
                        if (updateMessage != null)
                        {
                            var messageView = Utils.CreateMessageView(updateMessage);
                            View.Thread.Append(messageView);
                            View.Reload();
                        }
                    }
                    else if (SelectedThread is SignalGroup selectedGroup)
                    {
                        if (selectedGroup.GroupMemberships.FindAll((gm) => gm.Contact.ThreadId == conversation.ThreadId).Count > 0) // A group member was edited
                        {
                            View.Reload();
                        }
                    }
                }
                uiConversation.UpdateUI?.Invoke();
            }
            RepositionConversation(uiConversation);
        }

        public AppendResult HandleMessage(SignalMessage message, SignalConversation conversation)
        {
            AppendResult result = null;
            var localConversation = ConversationsDictionary[conversation.ThreadId];
            localConversation.LastMessage = message;
            localConversation.MessagesCount = conversation.MessagesCount;
            localConversation.LastActiveTimestamp = conversation.LastActiveTimestamp;
            localConversation.UnreadCount = conversation.UnreadCount;
            localConversation.LastSeenMessageIndex = conversation.LastSeenMessageIndex;
            localConversation.ExpiresInSeconds = conversation.ExpiresInSeconds;
            if (SelectedThread != null && SelectedThread == localConversation)
            {
                var messageView = Utils.CreateMessageView(message);
                result = View.Thread.Append(messageView);
            }
            RepositionConversation(localConversation);
            localConversation.UpdateUI?.Invoke();
            return result;
        }

        public void HandleMessageRead(SignalConversation updatedConversation)
        {
            var localConversation = ConversationsDictionary[updatedConversation.ThreadId];
            localConversation.LastSeenMessageIndex = updatedConversation.LastSeenMessageIndex;
            localConversation.UnreadCount = updatedConversation.UnreadCount;
            localConversation.UpdateUI();
        }

        public void HandleIdentitykeyChange(LinkedList<SignalMessage> messages)
        {
            foreach(var message in messages)
            {
                var conversation = ConversationsDictionary[message.ThreadId];
                conversation.MessagesCount += 1;
                conversation.UnreadCount += 1;
                HandleMessage(message, conversation);
            }
        }

        public void HandleMessageUpdate(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
            {
                View.Thread.UpdateMessageBox(updatedMessage);
            }
        }

        public void ReplaceConversationList(List<SignalConversation> conversations)
        {
            ConversationsDictionary.Clear();
            Conversations.Clear();
            Conversations.AddRange(conversations);
            foreach (var c in Conversations)
            {
                ConversationsDictionary.Add(c.ThreadId, c);
            }
            if (RequestedConversationId != null && RequestedConversationId != "")
            {
                Logger.LogDebug("RequestedConversationId is != null, refreshing");
                TrySelectConversation(RequestedConversationId);
                RequestedConversationId = null;
            }
            else if (SelectedThread != null)
            {
                Logger.LogDebug("SelectedThread is != null, refreshing");
                SelectedThread = ConversationsDictionary[SelectedThread.ThreadId];
                TrySelectConversation(SelectedThread.ThreadId);
            }
        }

        public void HandleAttachmentStatusChanged(SignalAttachment sa)
        {
            Logger.LogInformation("MPVM received attachment status update! {0}", sa.Status);
            if (SelectedThread != null && SelectedThread.ThreadId == sa.Message.ThreadId)
            {
                View.Thread.UpdateAttachment(sa);
            }
        }

        public void HandleBlockedContacts(List<SignalContact> blockedContacts)
        {
            // Signal sends a list of all blocked contacts, so if a blocked contacted was
            // unblocked it won't appear in the list anymore. So, unblock any blocked
            // contacts as well.
            foreach (var conversation in ConversationsDictionary)
            {
                // "as" is more efficient than "is" and a cast because "as" only needs to
                // type check once
                if (conversation.Value is SignalContact conversationContact)
                {
                    if (blockedContacts.FirstOrDefault(c => c.ThreadId == conversationContact.ThreadId) != null)
                    {
                        // Check if the contact is blocked and if so make sure it's blocked
                        if (!conversationContact.Blocked)
                        {
                            conversationContact.Blocked = true;
                            conversationContact.UpdateUI();
                        }
                    }
                    else
                    {
                        // If the contact is not in the blocked list then check if it's blocked
                        // and if so, unblock it.
                        if (conversationContact.Blocked)
                        {
                            conversationContact.Blocked = false;
                            conversationContact.UpdateUI();
                        }
                    }
                }
            }
        }
        #endregion
    }
}