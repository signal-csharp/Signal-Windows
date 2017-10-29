using Signal_Windows.Lib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib.Events
{
    public class UpdateMessageStatusEventArgs : EventArgs
    {
        public SignalMessage Message { get; private set; }

        public UpdateMessageStatusEventArgs(SignalMessage message)
        {
            Message = message;
        }
    }
}
