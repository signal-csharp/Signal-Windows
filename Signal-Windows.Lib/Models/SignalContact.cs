using System.Collections.Generic;

namespace Signal_Windows.Lib.Models
{
    public class SignalContact : SignalConversation
    {
        public SignalContact()
        {
            ThreadDisplayName = "Anonymous";
            Color = "";
            ThreadId = "";
        }

        public string Color { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; }
    }
}