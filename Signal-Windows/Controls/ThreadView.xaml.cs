using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class ThreadView : UserControl
    {
        public RangeObservableCollection<SignalMessage> Messages = new RangeObservableCollection<SignalMessage>();
        private Dictionary<ulong, MessageBox> OutgoingCache = new Dictionary<ulong, MessageBox>();

        public ThreadView()
        {
            this.InitializeComponent();
            InputTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        public MainPageViewModel GetMainPageVm()
        {
            return DataContext as MainPageViewModel;
        }

        public void Update(SignalThread thread)
        {
            InputTextBox.IsEnabled = thread.CanReceive;
        }

        public void ScrollToBottom()
        {
            SelectedMessagesScrollViewer.UpdateLayout();
            SelectedMessagesScrollViewer.ChangeView(0.0f, double.MaxValue, 1.0f, true);
        }

        public async Task Load(SignalThread thread)
        {
            InputTextBox.IsEnabled = false;
            DisposeCurrentThread();
            UnselectedBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            TitleBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            SelectedMessagesScrollViewer.Visibility = Windows.UI.Xaml.Visibility.Visible;
            InputTextBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
            var before = Util.CurrentTimeMillis();
            var messages = await Task.Run(() =>
            {
                return SignalDBContext.GetMessagesLocked(thread);
            });
            var after1 = Util.CurrentTimeMillis();
            Messages.AddRange(messages);
            var after2 = Util.CurrentTimeMillis();
            Debug.WriteLine("db query: " + (after1 - before));
            Debug.WriteLine("ui: " + (after2 - after1));
            InputTextBox.IsEnabled = thread.CanReceive;
        }

        public void DisposeCurrentThread()
        {
            Messages.Clear();
            OutgoingCache.Clear();
            UnselectedBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            TitleBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            SelectedMessagesScrollViewer.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            InputTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        public void UpdateMessageBox(SignalMessage updatedMessage)
        {
            if (OutgoingCache.ContainsKey(updatedMessage.Id))
            {
                var m = OutgoingCache[updatedMessage.Id];
                m.UpdateSignalMessageStatusIcon(updatedMessage);
            }
        }

        public void Append(SignalMessage sm)
        {
            Messages.Add(sm);
            //TODO move scrolltobottom here
        }

        public void AddToCache(SignalMessage sm)
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
    }
}