using Signal_Windows.Lib.Models;

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
