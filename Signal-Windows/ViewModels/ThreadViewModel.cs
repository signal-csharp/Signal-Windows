using GalaSoft.MvvmLight;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Signal_Windows.Controls;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    public class ThreadViewModel : ViewModelBase
    {
        public MainPageViewModel MainPageVm;
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();
        private Dictionary<long, MessageBox> OutgoingCache = new Dictionary<long, MessageBox>();

        public ThreadViewModel(MainPageViewModel mainPageVm)
        {
            this.MainPageVm = mainPageVm;
        }

        public async Task Load(SignalThread thread)
        {
            DisposeCurrentThread();
            ThreadTitle = thread.ThreadDisplayName;
            var before = Util.CurrentTimeMillis();
            var messages = await Task.Run(() =>
            {
                lock (SignalDBContext.DBLock)
                {
                    using (var ctx = new SignalDBContext())
                    {
                        return ctx.Messages
                            .Where(m => m.ThreadID == thread.ThreadId)
                            .Include(m => m.Author)
                            .Include(m => m.Attachments)
                            .AsNoTracking().ToList();
                    }
                }
            });
            var after1 = Util.CurrentTimeMillis();
            foreach (var m in messages)
            {
                Messages.Add(m);
            }
            var after2 = Util.CurrentTimeMillis();
            Debug.WriteLine("db query: " + (before - after1));
            Debug.WriteLine("ui: " + (after1 - after2));
        }

        private void DisposeCurrentThread()
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
            OutgoingCache[sm.Id] = sm.View;
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
    }
}