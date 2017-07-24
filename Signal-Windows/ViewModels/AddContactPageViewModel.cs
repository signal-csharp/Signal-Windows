using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.ApplicationModel.Contacts;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Signal_Windows.Views;
using Windows.UI.Core;

namespace Signal_Windows.ViewModels
{
    public class AddContactPageViewModel : ViewModelBase
    {
        public MainPageViewModel MainPageVM;
        public AddContactPage View;
        private ImageSource _ContactPhoto = null;

        public ImageSource ContactPhoto
        {
            get { return _ContactPhoto; }
            set { _ContactPhoto = value; RaisePropertyChanged(nameof(ContactPhoto)); }
        }

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

        private bool _UIEnabled = true;
        public bool UIEnabled
        {
            get { return _UIEnabled; }
            set { _UIEnabled = value; RaisePropertyChanged(nameof(UIEnabled)); }
        }

        internal void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            if(UIEnabled)
            {
                View.Frame.GoBack();
            }
        }

        internal async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if(UIEnabled)
            {
                UIEnabled = false;
                Debug.WriteLine("creating contact {0} ({1})", ContactName, ContactNumber);
                SignalContact contact = new SignalContact()
                {
                    ThreadDisplayName = ContactName,
                    ThreadId = ContactNumber,
                    CanReceive = true,
                    AvatarFile = null,
                    LastActiveTimestamp = 0,
                    Draft = null,
                    Color = "red",
                    Unread = 0
                };
                ContactName = "";
                ContactNumber = "";
                await Task.Run(() =>
                {
                    SignalDBContext.InsertOrUpdateContactLocked(contact, MainPageVM);
                });
                UIEnabled = true;
            }
        }

        internal async void PickButton_Click(object sender, RoutedEventArgs e)
        {
            if (UIEnabled)
            {
                UIEnabled = false;
                ContactPicker contactPicker = new ContactPicker();
                contactPicker.SelectionMode = ContactSelectionMode.Fields;
                contactPicker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);
                var contact = await contactPicker.PickContactAsync();
                if (contact != null)
                {
                    // The contact we just got doesn't contain the contact picture so we need to fetch it
                    // see https://stackoverflow.com/questions/33401625/cant-get-contact-profile-images-in-uwp
                    ContactStore contactStore = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
                    // If we do not have access to contacts the ContactStore will be null, we can still use the contact the user
                    // seleceted however
                    if (contactStore != null)
                    {
                        Contact realContact = await contactStore.GetContactAsync(contact.Id);
                        if (realContact.SourceDisplayPicture != null)
                        {
                            using (var stream = await realContact.SourceDisplayPicture.OpenReadAsync())
                            {
                                BitmapImage bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(stream);
                                ContactPhoto = bitmapImage;
                            }
                        }
                        else
                        {
                            ContactPhoto = null;
                        }
                    }
                    ContactName = contact.Name;
                    if (contact.Phones.Count > 0)
                    {
                        var originalNumber = contact.Phones[0].Number;
                        if (originalNumber[0] != '+')
                        {
                            // need a better way of determining the "default" country code here
                            var formattedPhoneNumber = PhoneNumberFormatter.formatE164("1", originalNumber);
                            if (string.IsNullOrEmpty(formattedPhoneNumber))
                            {
                                ContactNumber = originalNumber;
                                MessageDialog message = new MessageDialog("Please format the number in E.164 format.", "Could not format number");
                                await message.ShowAsync();
                            }
                            else
                            {
                                ContactNumber = formattedPhoneNumber;
                            }
                        }
                    }
                }
                UIEnabled = true;
            }
        }
    }
}