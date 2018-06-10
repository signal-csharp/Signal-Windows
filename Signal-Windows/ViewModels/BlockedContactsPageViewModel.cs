using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using Signal_Windows.Models;
using Signal_Windows.Storage;

namespace Signal_Windows.ViewModels
{
    public class BlockedContactsPageViewModel : ViewModelBase
    {
        public ObservableCollection<SignalContact> BlockedContacts { get; set; } = new ObservableCollection<SignalContact>();

        public bool NoBlockedContacts
        {
            get
            {
                if (BlockedContacts != null)
                {
                    return BlockedContacts.Count == 0;
                }
                else
                {
                    return true;
                }
            }
            set
            {
            }
        }

        public void OnNavigatedTo()
        {
            List<SignalContact> blockedContacts = SignalDBContext.GetAllContactsLocked()
                .Where(contact => contact.Blocked)
                .ToList();
            BlockedContacts = new ObservableCollection<SignalContact>(blockedContacts);
        }
    }
}
