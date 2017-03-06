using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_UWP.Models
{
    public class SignalContact
    {
        public SignalContact()
        {
            ContactName = "Anonymous";
            Color = "";
            E164Number = "";
        }
        public uint Id { get; set; }
        public string E164Number { get; set; }
        public string ContactName { get; set; }
        public string Color { get; set; }
    }
}
