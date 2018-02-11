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
using System.Threading;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;

namespace Signal_Windows.ViewModels
{
    public class AddContactPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<AddContactPageViewModel>();
        public ObservableCollection<PhoneContact> Contacts;
        private List<PhoneContact> signalContacts;
        private PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
        public MainPageViewModel MainPageVM;
        public AddContactPage View;

        private string _ContactName = string.Empty;
        public string ContactName
        {
            get { return _ContactName; }
            set { _ContactName = value; RaisePropertyChanged(nameof(ContactName)); }
        }

        private string _ContactNumber = string.Empty;
        public string ContactNumber
        {
            get { return _ContactNumber; }
            set { _ContactNumber = value; RaisePropertyChanged(nameof(ContactNumber)); }
        }

        private bool _ContactsVisible = true;
        public bool ContactsVisible
        {
            get { return _ContactsVisible; }
            set { _ContactsVisible = value; RaisePropertyChanged(nameof(ContactsVisible)); }
        }

        private bool _RefreshingContacts = false;
        public bool RefreshingContacts
        {
            get { return _RefreshingContacts; }
            set { _RefreshingContacts = value; RaisePropertyChanged(nameof(RefreshingContacts)); }
        }

        public AddContactPageViewModel()
        {
            Contacts = new ObservableCollection<PhoneContact>();
            signalContacts = new List<PhoneContact>();
        }

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

        public async Task OnNavigatedTo(CancellationToken? cancellationToken = null)
        {
            ContactName = string.Empty;
            ContactNumber = string.Empty;
            await RefreshContacts(cancellationToken);
        }

        public async Task RefreshContacts(CancellationToken? cancellationToken = null)
        {
            RefreshingContacts = true;
            Contacts.Clear();
            signalContacts.Clear();
            SignalServiceAccountManager accountManager = new SignalServiceAccountManager(LibUtils.ServiceUrls, SignalLibHandle.Instance.Store.Username, SignalLibHandle.Instance.Store.Password, (int)SignalLibHandle.Instance.Store.DeviceId, LibUtils.USER_AGENT);
            ContactStore contactStore = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
            List<PhoneContact> intermediateContacts = new List<PhoneContact>();
            if (contactStore != null)
            {
                HashSet<string> seenNumbers = new HashSet<string>();
                var contacts = await contactStore.FindContactsAsync();
                ContactAnnotationStore contactAnnotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                ContactAnnotationList contactAnnotationList;
                var contactAnnotationLists = await contactAnnotationStore.FindAnnotationListsAsync();
                if (contactAnnotationLists.Count == 0)
                {
                    contactAnnotationList = await contactAnnotationStore.CreateAnnotationListAsync();
                }
                else
                {
                    contactAnnotationList = contactAnnotationLists[0];
                }
                
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
                                Logger.LogDebug("RefreshContacts() could not parse number");
                                continue;
                            }
                            if (!seenNumbers.Contains(formattedNumber))
                            {
                                seenNumbers.Add(formattedNumber);
                                PhoneContact phoneContact = new PhoneContact
                                {
                                    Id = contact.Id,
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
                                intermediateContacts.Add(phoneContact);
                            }
                        }
                    }
                }

                // check if we've annotated a contact as a Signal contact already, if we have we don't need to ask Signal about them
                for (int i = 0; i < intermediateContacts.Count; i++)
                {
                    var annotatedContact = await contactAnnotationList.FindAnnotationsByRemoteIdAsync(intermediateContacts[i].PhoneNumber);
                    if (annotatedContact.Count > 0)
                    {
                        intermediateContacts[i].OnSignal = true;
                        signalContacts.Add(intermediateContacts[i]);
                        intermediateContacts.RemoveAt(i);
                        i--;
                    }
                }

                var signalContactDetails = accountManager.getContacts(intermediateContacts.Select(c => c.PhoneNumber).ToList());
                foreach (var contact in intermediateContacts)
                {
                    var foundContact = signalContactDetails.FirstOrDefault(c => c.getNumber() == contact.PhoneNumber);
                    if (foundContact != null)
                    {
                        contact.OnSignal = true;
                        ContactAnnotation contactAnnotation = new ContactAnnotation();
                        contactAnnotation.ContactId = contact.Id;
                        contactAnnotation.RemoteId = contact.PhoneNumber;
                        contactAnnotation.SupportedOperations = ContactAnnotationOperations.Message | ContactAnnotationOperations.ContactProfile;
                        await contactAnnotationList.TrySaveAnnotationAsync(contactAnnotation);
                        signalContacts.Add(contact);
                    }
                }
                Contacts.AddRange(signalContacts);
            }
            else
            {
                ContactsVisible = false;
            }
            RefreshingContacts = false;
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
                var validContacts = GetContactsMatchingText(text, signalContacts).ToList();
                Contacts.Clear();
                Contacts.AddRange(validContacts);
            }
        }

        private IEnumerable<PhoneContact> GetContactsMatchingText(string text, List<PhoneContact> contacts)
        {
            return contacts.Where(
                c => c.Name.ContainsCaseInsensitive(text) ||
                c.PhoneNumber.ContainsCaseInsensitive(text));
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
                Color = Utils.CalculateDefaultColor(name),
                UnreadCount = 0
            };
            await Task.Run(async () =>
            {
                //TODO inform UI
                await SignalLibHandle.Instance.AddContact(number);
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
            PhoneNumber phoneNumber = phoneNumberUtil.Parse(number, Utils.GetCountryISO());
            return phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
        }
    }
}
