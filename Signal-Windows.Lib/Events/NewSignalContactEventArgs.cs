using Signal_Windows.Lib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib.Events
{
    public class NewSignalContactEventArgs : EventArgs
    {
        public SignalContact Contact { get; private set; }

        public NewSignalContactEventArgs(SignalContact contact)
        {
            Contact = contact;
        }
    }
}
