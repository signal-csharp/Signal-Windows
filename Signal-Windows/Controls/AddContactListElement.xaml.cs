using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Signal_Windows.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Contacts;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class AddContactListElement : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public AddContactListElement()
        {
            this.InitializeComponent();
            this.DataContextChanged += AddContactListElement_DataContextChanged;
        }

        public string _DisplayName;
        public string DisplayName
        {
            get { return _DisplayName; }
            set
            {
                _DisplayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public string _PhoneNumber;
        public string PhoneNumber
        {
            get { return _PhoneNumber; }
            set
            {
                _PhoneNumber = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhoneNumber)));
            }
        }

        public BitmapImage _ContactPhoto = null;
        public BitmapImage ContactPhoto
        {
            get { return _ContactPhoto; }
            set
            {
                _ContactPhoto = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContactPhoto)));
            }
        }

        public bool _OnSignal;
        public bool OnSignal
        {
            get { return _OnSignal; }
            set
            {
                _OnSignal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnSignal)));
            }
        }

        public Contact _Contact;
        public Contact Contact
        {
            get { return _Contact; }
            set
            {
                _Contact = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Contact)));
            }
        }

        public PhoneContact Model
        {
            get
            {
                return DataContext as PhoneContact;
            }
            set
            {
                DataContext = value;
            }
        }

        private void AddContactListElement_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                Model.View = this;
                DisplayName = Model.Name;
                PhoneNumber = Model.PhoneNumber;
                if (Model.Photo == null)
                {
                    BitmapImage image = new BitmapImage(new Uri("ms-appx:///Assets/gambino.png"));
                    ContactPhoto = image;
                }
                else
                {
                    ContactPhoto = Model.Photo;
                }
                OnSignal = Model.OnSignal;
                Contact = Model.Contact;
            }
        }
    }
}
