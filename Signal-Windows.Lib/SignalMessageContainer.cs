using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib
{
    public class SignalMessageContainer
    {
        public SignalMessage Message;
        public int Index;
        public SignalMessageContainer(SignalMessage message, int index)
        {
            Message = message;
            Index = index;
        }
    }
}
