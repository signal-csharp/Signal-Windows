using System;
using Windows.UI.Xaml.Media;

namespace Signal_Windows.Models
{
    // Not a database model.
    public class PhoneContact
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public Guid? SignalGuid { get; set; }
        public ImageSource Photo { get; set; }
        public bool OnSignal { get; set; }
    }
}
