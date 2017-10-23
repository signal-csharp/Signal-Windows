using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Signal_Windows.Controls;
using Windows.UI.Xaml.Media;
using Windows.ApplicationModel.Contacts;
using Windows.UI.Xaml.Media.Imaging;

namespace Signal_Windows.Models
{
    public class PhoneContact
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public BitmapImage Photo { get; set; }
        public bool OnSignal { get; set; }
        public Contact Contact { get; set; }
        public AddContactListElement View;
    }
}
