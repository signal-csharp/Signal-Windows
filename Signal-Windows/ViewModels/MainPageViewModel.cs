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

        private async Task<bool> SendMessage(string messageText)
        {
            Debug.WriteLine("starting sendmessage");
            try
            {
                if (!string.IsNullOrEmpty(messageText))
                {
                    var now = Util.CurrentTimeMillis();
                    SignalMessage message = new SignalMessage()
                    {
                        Author = null,
                        ComposedTimestamp = now,
                        Content = new SignalMessageContent() { Content = messageText },
                        ThreadId = SelectedThread.ThreadId,
                        ReceivedTimestamp = now,
                        Direction = SignalMessageDirection.Outgoing,
                        Read = true,
                        Type = SignalMessageType.Normal
                    };
                    await App.Handle.SendMessage(message, SelectedThread);
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
                    int index = Conversations.IndexOf(uiConversation);
                    Logger.LogDebug("RepositionConversation() moving conversation from {0} to {1}", index, i);
                    Conversations.Move(index, i);
                    break;
                }
            }
        }

        internal async Task SendMessageButton_Click(TextBox messageTextBox)
        {
            bool sendMessageResult = await SendMessage(messageTextBox.Text.Replace("\r", "\r\n"));
            if (sendMessageResult)
            {
                messageTextBox.Text = string.Empty;
            }
            messageTextBox.Focus(FocusState.Programmatic);
        }

        public void TrySelectConversation(string conversationId)
        {
            if (ConversationsDictionary.ContainsKey(conversationId))
            {
                SelectedConversation = ConversationsDictionary[conversationId];
            }
            else
            {
                Logger.LogError("TrySelectConversation could not select conversation: key is not present");
            }
        }

        public void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                Logger.LogDebug("ContactsList_SelectionChanged()");
                WelcomeVisibility = Visibility.Collapsed;
                ThreadVisibility = Visibility.Visible;
                SelectedThread = SelectedConversation;
                View.Thread.Load(SelectedThread);
                View.SwitchToStyle(View.GetCurrentViewStyle());
            }
        }

        #region SignalFrontend API
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
                            var container = new SignalMessageContainer(updateMessage, (int)SelectedThread.MessagesCount - 1);
                            View.Thread.Append(container);
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
            localConversation.UpdateUI();
            if (SelectedThread != null && SelectedThread == localConversation)
            {
                var container = new SignalMessageContainer(message, (int)SelectedThread.MessagesCount - 1);
                result = View.Thread.Append(container);
            }
            RepositionConversation(localConversation);
            return result;
        }

        public void HandleMessageRead(long messageIndex, SignalConversation conversation)
        {
            var localConversation = ConversationsDictionary[conversation.ThreadId];
            Logger.LogTrace("LastSeenMessageIndex = {0}", messageIndex);
            localConversation.LastSeenMessageIndex = messageIndex;
            localConversation.UnreadCount = conversation.UnreadCount;
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
        #endregion
    }
}