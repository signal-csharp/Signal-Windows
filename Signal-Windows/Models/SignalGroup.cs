using System.Collections.Generic;

namespace Signal_Windows.Models
{
    public class SignalGroup : SignalThread
    {
        public List<GroupMembership> GroupMemberships { get; set; }
    }

    public class GroupMembership
    {
        public ulong Id { get; set; }
        public ulong GroupId { get; set; }
        public SignalGroup Group { get; set; }
        public ulong ContactId { get; set; }
        public SignalContact Contact { get; set; }
    }
}