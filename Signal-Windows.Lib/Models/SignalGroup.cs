using System.Collections.Generic;

namespace Signal_Windows.Models
{
    // Database model
    public class SignalGroup : SignalConversation
    {
        public List<GroupMembership> GroupMemberships { get; set; }
    }

    public class GroupMembership
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public SignalGroup Group { get; set; }
        public long ContactId { get; set; }
        public SignalContact Contact { get; set; }
    }
}