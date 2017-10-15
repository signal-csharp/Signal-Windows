using GalaSoft.MvvmLight;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Core;
using libsignalservice;
using PhoneNumbers;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml.Controls;
using System.Globalization;

namespace Signal_Windows.ViewModels
{
    public class AddContactPageViewModel : ViewModelBase
    {
        public ObservableCollection<PhoneContact> Contacts;
        private List<PhoneContact> contactList;
        private PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        private string _ContactName = "";
        public string ContactName
        {
            get { return _ContactName; }
            set { _ContactName = value; RaisePropertyChanged(nameof(ContactName)); }
        }

        private string _ContactNumber = "";
        public string ContactNumber
        {
            get { return _ContactNumber; }
            set { _ContactNumber = value; RaisePropertyChanged(nameof(ContactNumber)); }
        }

        public AddContactPageViewModel()
        {
            Contacts = new ObservableCollection<PhoneContact>();
            contactList = new List<PhoneContact>();
        }

        public async Task OnNavigatedTo()
        {
            SignalServiceAccountManager accountManager = new SignalServiceAccountManager(App.ServiceUrls, App.Store.Username, App.Store.Password, (int)App.Store.DeviceId, App.USER_AGENT);
            ContactStore contactStore = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
            if (contactStore != null)
            {
                HashSet<string> seenNumbers = new HashSet<string>();
                var contacts = await contactStore.FindContactsAsync();
                foreach (var contact in contacts)
                {
                    var phones = contact.Phones;
                    foreach (var phone in contact.Phones)
                    {
                        if (phone.Kind == ContactPhoneKind.Mobile)
                        {
                            string formattedNumber = null;
                            try
                            {
                                formattedNumber = ParsePhoneNumber(phone.Number);
                            }
                            catch (NumberParseException)
                            {
                                Debug.WriteLine($"Couldn't parse {phone.Number}");
                                continue;
                            }
                            if (!seenNumbers.Contains(formattedNumber))
                            {
                                seenNumbers.Add(formattedNumber);
                                PhoneContact phoneContact = new PhoneContact
                                {
                                    Name = contact.FullName,
                                    PhoneNumber = formattedNumber,
                                    OnSignal = false
                                };
                                if (contact.SourceDisplayPicture != null)
                                {
                                    using (var stream = await contact.SourceDisplayPicture.OpenReadAsync())
                                    {
                                        BitmapImage bitmapImage = new BitmapImage();
                                        await bitmapImage.SetSourceAsync(stream);
                                        phoneContact.Photo = bitmapImage;
                                    }
                                }
                                contactList.Add(phoneContact);
                            }
                        }
                    }
                }

                var signalContactDetails = accountManager.getContacts(contactList.Select(c => c.PhoneNumber).ToList());
                foreach (var contact in contactList)
                {
                    var foundContact = signalContactDetails.FirstOrDefault(c => c.getNumber() == contact.PhoneNumber);
                    if (foundContact != null)
                    {
                        contact.OnSignal = true;
                    }
                    Contacts.Add(contact);
                }
            }
            else
            {
                // something something we don't have contact access
            }
        }

        public MainPageViewModel MainPageVM;
        public AddContactPage View;

        private bool _UIEnabled = true;
        public bool UIEnabled
        {
            get { return _UIEnabled; }
            set { _UIEnabled = value; RaisePropertyChanged(nameof(UIEnabled)); }
        }

        private bool _AddEnabled = false;
        public bool AddEnabled
        {
            get { return _AddEnabled; }
            set { _AddEnabled = value; RaisePropertyChanged(nameof(AddEnabled)); }
        }

        private bool validName = false;
        private bool ValidName
        {
            get { return validName; }
            set
            {
                validName = value;
                SetAddEnabled();
            }
        }

        private bool validNumber = false;
        private bool ValidNumber
        {
            get { return validNumber; }
            set
            {
                validNumber = value;
                SetAddEnabled();
            }
        }

        private void SetAddEnabled()
        {
            AddEnabled = ValidName && ValidNumber && UIEnabled;
        }

        internal void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            if (UIEnabled)
            {
                View.Frame.GoBack();
                e.Handled = true;
            }
        }

        internal void searchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                string text = sender.Text;
                var validContacts = contactList.Where(
                    c => c.Name.ContainsCaseInsensitive(text) ||
                    c.PhoneNumber.ContainsCaseInsensitive(text));
                Contacts.Clear();
                foreach (var contact in validContacts)
                {
                    Contacts.Add(contact);
                }
            }
        }

        internal void ContactNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string text = textBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                ValidName = false;
            }
            else
            {
                ValidName = true;
            }
        }

        internal void ContactNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: See the TODO for AddButton_Click
            TextBox textBox = sender as TextBox;
            string text = textBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                ValidNumber = false;
            }
            else
            {
                ValidNumber = true;
            }
        }

        // TODO: use the AsYouTypeFormatter when typing into the ContactNumber box so we don't have to validate here
        // we need to be sure that the number here is valid
        internal async Task AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (UIEnabled)
            {
                UIEnabled = false;
                string formattedPhoneNumber = null;
                try
                {
                    formattedPhoneNumber = ParsePhoneNumber(ContactNumber);
                }
                catch (NumberParseException)
                {
                    MessageDialog message = new MessageDialog("Please format the number in E.164 format.", "Could not format number");
                    await message.ShowAsync();
                    return;
                }
                await AddContact(ContactName, formattedPhoneNumber);
                UIEnabled = true;
            }
        }

        internal async Task ContactsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (UIEnabled)
            {
                UIEnabled = false;
                PhoneContact phoneContact = e.ClickedItem as PhoneContact;
                await AddContact(phoneContact.Name, phoneContact.PhoneNumber);
                UIEnabled = true;
            }
        }

        private async Task AddContact(string name, string number)
        {
            Debug.WriteLine("creating contact {0} ({1})", name, number);
            SignalContact contact = new SignalContact()
            {
                ThreadDisplayName = name,
                ThreadId = number,
                CanReceive = true,
                AvatarFile = null,
                LastActiveTimestamp = 0,
                Draft = null,
                Color = "red",
                UnreadCount = 0
            };
            await Task.Run(() =>
            {
                SignalDBContext.InsertOrUpdateContactLocked(contact, MainPageVM);
            });
        }

        /// <summary>
        /// Parses and formats a number in E164 format
        /// </summary>
        /// <param name="number">The number to parse</param>
        /// <exception cref="NumberParseException"></exception>
        /// <returns>A number in E164 format</returns>
        private string ParsePhoneNumber(string number)
        {
            // on phone we should try to get their SIM country code
            // otherwise we should try to use the user's location?
            PhoneNumber phoneNumber = phoneNumberUtil.Parse(number, "US");
            return phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
        }
    }
}