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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
        private StorageFile SelectedFile;

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

        private Brush _HeaderBackground;

        public Brush HeaderBackground
        {
            get { return _HeaderBackground; }
            set { _HeaderBackground = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderBackground))); }
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

        private bool spellCheckEnabled;
        public bool SpellCheckEnabled
        {
            get { return spellCheckEnabled; }
            set { spellCheckEnabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpellCheckEnabled))); }
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
            SendButtonEnabled = false;
            ResetInput();
            UserInputBar.FocusTextBox();
            DisposeCurrentThread();
            UpdateHeader(conversation);
            SpellCheckEnabled = GlobalSettingsManager.SpellCheckSetting;

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
            SendButtonEnabled = conversation.CanReceive;
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

        public AppendResult Append(IMessageView sm)
        {
            AppendResult result = new AppendResult(false);
            bool bottom = GetBottommostIndex() == Collection.Count - 2; // -2 because we already incremented Count
            Collection.Add(sm, sm.Model.Author == null);
            if (bottom)
            {
                UpdateLayout();
                ScrollToBottom();
                if (ActivationState != CoreWindowActivationState.Deactivated)
                {
                    result = new AppendResult(true);
                }
            }
            return result;
        }

        private async void UserInputBar_OnEnterKeyPressed()
        {
            bool shift = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            if (shift)
            {
                UserInputBar.AddLinefeed();
            }
            else
            {
                bool sendMessageResult = await GetMainPageVm().SendMessage(UserInputBar.InputText, SelectedFile);
                if (sendMessageResult)
                {
                    ResetInput();
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


        private async void UserInputBar_OnSendMessageButtonClicked()
        {
            if (string.IsNullOrEmpty(UserInputBar.InputText) && SelectedFile == null)
            {
                var filePicker = new FileOpenPicker();
                filePicker.FileTypeFilter.Add("*"); // Without this the file picker throws an exception, this is not documented
                SelectedFile = await filePicker.PickSingleFileAsync();
                if (SelectedFile != null)
                {
                    AddedAttachmentDisplay.ShowAttachment(SelectedFile.Name);
                    UpdateSendButtonIcon();
                    UserInputBar.FocusTextBox();
                }
                else
                {
                    AddedAttachmentDisplay.HideAttachment();
                }
            }
            else
            {
                bool sendMessageResult = await GetMainPageVm().SendMessage(UserInputBar.InputText, SelectedFile);
                if (sendMessageResult)
                {
                    ResetInput();
                }
            }
        }

        private static ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer)
            {
                return (ScrollViewer)element;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);

                var result = GetScrollViewer(child);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }

            return null;
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
                        await App.Handle.SetMessageRead(msg);
                    });
                }
            }
        }

        private async void UserInputBar_OnUnblockButtonClicked()
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

        private void AddedAttachmentDisplay_OnCancelAttachmentButtonClicked()
        {
            AddedAttachmentDisplay.HideAttachment();
            SelectedFile = null;
            UpdateSendButtonIcon();
        }

        private void UpdateSendButtonIcon()
        {
            if (UserInputBar.InputText != string.Empty || SelectedFile != null)
            {
                UserInputBar.SetSendButtonIcon(Symbol.Send);
            }
            else
            {
                UserInputBar.SetSendButtonIcon(Symbol.Attach);
            }
        }

        private void ResetInput()
        {
            SelectedFile = null;
            UserInputBar.InputText = string.Empty;
            UpdateSendButtonIcon();
            AddedAttachmentDisplay.HideAttachment();
        }

        private async void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.V)
            {
                bool ctrl = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                if (ctrl)
                {
                    var dataPackageView = Clipboard.GetContent();
                    if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                    {
                        var pastedFiles = await dataPackageView.GetStorageItemsAsync();
                        var pastedFile = pastedFiles[0];
                        SelectedFile = pastedFile as StorageFile;
                        AddedAttachmentDisplay.ShowAttachment(SelectedFile.Name);
                        UpdateSendButtonIcon();
                    }
                    else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                    {
                        RandomAccessStreamReference pastedBitmap = await dataPackageView.GetBitmapAsync();
                        var pastedBitmapStream = await pastedBitmap.OpenReadAsync();
                        var tmpFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("Signal-Windows-Screenshot.png", CreationCollisionOption.GenerateUniqueName);
                        using (var tmpFileStream = await tmpFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(pastedBitmapStream);
                            var pixels = await decoder.GetPixelDataAsync();
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, tmpFileStream);
                            encoder.SetPixelData(decoder.BitmapPixelFormat,
                                BitmapAlphaMode.Ignore, // Alpha is not used
                                decoder.OrientedPixelWidth,
                                decoder.OrientedPixelHeight,
                                decoder.DpiX, decoder.DpiY,
                                pixels.DetachPixelData());
                            await encoder.FlushAsync();
                        }
                        SelectedFile = tmpFile;
                        AddedAttachmentDisplay.ShowAttachment(SelectedFile.Name);
                        UpdateSendButtonIcon();
                    }
                }
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await e.DataView.GetStorageItemsAsync();
                var storageItem = storageItems[0];
                SelectedFile = storageItem as StorageFile;
                if (SelectedFile != null)
                {
                    AddedAttachmentDisplay.ShowAttachment(SelectedFile.Name);
                    UpdateSendButtonIcon();
                }
            }
        }

        private void ConversationItemsControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var scrollbar = GetScrollViewer(this);
            if (scrollbar != null)
            {
                var verticalDelta = e.PreviousSize.Height - e.NewSize.Height;
                if (verticalDelta > 0)
                {
                    scrollbar.ChangeView(null, scrollbar.VerticalOffset + verticalDelta, null);
                }
            }
        }
    }
}