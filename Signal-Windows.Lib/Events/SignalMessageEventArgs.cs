using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Signal_Windows.Models;

namespace Signal_Windows.Lib.Events
{
    public class SignalMessageEventArgs : EventArgs
    {
        public SignalMessage Message { get; private set; }

        public SignalMessageEventArgs(SignalMessage message)
        {
            Message = message;
        }
    }
}
