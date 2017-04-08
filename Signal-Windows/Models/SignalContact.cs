using System.Collections.Generic;

namespace Signal_Windows.Models
{
    public class SignalContact : SignalThread
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