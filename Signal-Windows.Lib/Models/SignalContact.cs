using System.Collections.Generic;

namespace Signal_Windows.Models
{
    public class SignalContact : SignalConversation
    {
        public string Color { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; }
    }
}