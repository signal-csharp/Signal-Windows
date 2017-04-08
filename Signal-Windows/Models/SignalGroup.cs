using System.Collections.Generic;

namespace Signal_Windows.Models
{
    public class SignalGroup : SignalThread
    {
        public List<GroupMembership> GroupMemberships { get; set; }
    }

    public class GroupMembership
    {
        public uint Id { get; set; }
        public uint GroupId { get; set; }
        public SignalGroup Group { get; set; }
        public uint ContactId { get; set; }
        public SignalContact Contact { get; set; }
    }
}