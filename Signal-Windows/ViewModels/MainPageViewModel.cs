using GalaSoft.MvvmLight;
using libsignalservice;
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
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
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
                if (messageText != string.Empty)
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
                    await SignalLibHandle.Instance.SendMessage(message, SelectedThread);
                    Debug.WriteLine("keydown lock released");
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

        internal async Task SendMessageButton_Click(TextBox messageTextBox)
        {
            bool sendMessageResult = await SendMessage(messageTextBox.Text);
            if (sendMessageResult)
            {
                messageTextBox.Text = string.Empty;
            }
        }

        #region SignalFrontend API
        public void AddOrUpdateConversation(SignalConversation conversation)
        {
            if (!ConversationsDictionary.ContainsKey(conversation.ThreadId))
            {
                Conversations.Add(conversation);
                ConversationsDictionary.Add(conversation.ThreadId, conversation);
            }
            else
            {
                SignalConversation c = ConversationsDictionary[conversation.ThreadId];
                c.LastActiveTimestamp = conversation.LastActiveTimestamp;
                c.CanReceive = conversation.CanReceive;
                c.LastMessage = conversation.LastMessage;
                c.LastSeenMessage = conversation.LastSeenMessage;
                c.LastSeenMessageIndex = conversation.LastSeenMessageIndex;
                c.MessagesCount = conversation.MessagesCount;
                c.ThreadDisplayName = conversation.ThreadDisplayName;
                c.UnreadCount = conversation.UnreadCount;
                if (c is SignalContact ourContact && conversation is SignalContact newContact)
                {
                    ourContact.Color = newContact.Color;
                }
                c.UpdateUI?.Invoke();
            }
            SignalConversation uiConversation = ConversationsDictionary[conversation.ThreadId];
            for (int i = 0;i < Conversations.Count;i++)
            {
                var c = Conversations[i];
                if (c == uiConversation)
                {
                    break;
                }
                if (uiConversation.LastActiveTimestamp > c.LastActiveTimestamp)
                {
                    int index = Conversations.IndexOf(uiConversation);
                    Logger.LogDebug("AddOrUpdateConversation moving conversation from {0} to {1}", index, i);
                    Conversations.Move(index, i);
                    break;
                }
            }
        }

        public void HandleMessage(SignalMessage message)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
            {
                var container = new SignalMessageContainer(message, (int)SelectedThread.MessagesCount - 1);
                View.Thread.Append(container, false);
            }
            if (ApplicationView.GetForCurrentView().Id == App.MainViewId)
            {
                if (message.Author != null)
                {
                    SignalNotifications.TryVibrate(true);
                    SignalNotifications.SendMessageNotification(message);
                    SignalNotifications.SendTileNotification(message);
                }
            }
        }

        public void HandleIdentitykeyChange(LinkedList<SignalMessage> messages)
        {
            foreach(var message in messages)
            {
                var conversation = ConversationsDictionary[message.ThreadId];
                conversation.MessagesCount += 1;
                conversation.UnreadCount += 1;
                HandleMessage(message);
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
        }
        #endregion
    }
}