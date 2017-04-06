using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Models
{
    public class SignalGroup
    {
        public uint Id { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; }
        public string GroupDisplayName { get; set; }
        public string Color { get; set; }
        public long LastActiveTimestamp { get; set; }
        public string AvatarFile { get; set; }
        public uint Unread { get; set; }
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