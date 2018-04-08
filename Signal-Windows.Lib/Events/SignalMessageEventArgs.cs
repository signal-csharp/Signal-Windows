using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Signal_Windows.Models;

namespace Signal_Windows.Lib.Events
{
    public enum SignalMessageType
    {
        NormalMessage,
        PipeEmptyMessage
    }
    public class SignalMessageEventArgs : EventArgs
    {
        public SignalMessage Message { get; private set; }
        public SignalMessageType MessageType { get; private set; }

        public SignalMessageEventArgs(SignalMessage message, SignalMessageType type)
        {
            Message = message;
            MessageType = type;
        }
    }
}
