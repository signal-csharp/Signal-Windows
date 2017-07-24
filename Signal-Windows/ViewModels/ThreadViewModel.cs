using GalaSoft.MvvmLight;
using libsignalservice.util;
using Signal_Windows.Controls;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class ThreadViewModel : ViewModelBase
    {
        public MainPageViewModel MainPageVm;
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();
        private Dictionary<ulong, MessageBox> OutgoingCache = new Dictionary<ulong, MessageBox>();

        public ThreadViewModel(MainPageViewModel mainPageVm)
        {
            this.MainPageVm = mainPageVm;
            WelcomeVisibility = Visibility.Visible;
            MainVisibility = Visibility.Collapsed;
        }

        public void Update(SignalThread thread)
        {
            CanSend = thread.CanReceive;
        }

        public async Task Load(SignalThread thread)
        {
            CanSend = false;
            DisposeCurrentThread();
            WelcomeVisibility = Visibility.Collapsed;
            MainVisibility = Visibility.Visible;
            ThreadTitle = thread.ThreadDisplayName;
            var before = Util.CurrentTimeMillis();
            var messages = await Task.Run(() =>
            {
                return SignalDBContext.GetMessagesLocked(thread, this);
            });
            var after1 = Util.CurrentTimeMillis();
            foreach (var m in messages)
            {
                Messages.Add(m);
            }
            var after2 = Util.CurrentTimeMillis();
            Debug.WriteLine("db query: " + (after1 - before));
            Debug.WriteLine("ui: " + (after2 - after1));
            CanSend = thread.CanReceive;
        }

        public void DisposeCurrentThread()
        {
            Messages.Clear();
            OutgoingCache.Clear();
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
            if(sm.View != null)
            {
                OutgoingCache[sm.Id] = sm.View;
            }
            else
            {
                throw new Exception("Attempt to add null view to OutgoingCache");
            }
        }

        private string _ThreadTitle;

        public string ThreadTitle
        {
            get
            {
                return _ThreadTitle;
            }
            set
            {
                _ThreadTitle = value;
                RaisePropertyChanged("ThreadTitle");
            }
        }

        private Visibility _WelcomeVisibility;

        public Visibility WelcomeVisibility
        {
            get
            {
                return _WelcomeVisibility;
            }
            set
            {
                _WelcomeVisibility = value;
                RaisePropertyChanged("WelcomeVisibility");
            }
        }

        private Visibility _MainVisibility;

        public Visibility MainVisibility
        {
            get
            {
                return _MainVisibility;
            }
            set
            {
                _MainVisibility = value;
                RaisePropertyChanged("MainVisibility");
            }
        }

        private bool _CanSend;

        public bool CanSend
        {
            get
            {
                return _CanSend;
            }
            set
            {
                _CanSend = value;
                RaisePropertyChanged("CanSend");
            }
        }
    }
}