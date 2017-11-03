using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace Signal_Windows.Models
{
    public class PhoneContact
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public ImageSource Photo { get; set; }
        public bool OnSignal { get; set; }
    }
}
