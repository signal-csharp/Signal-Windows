using Signal_Windows.Controls;
using System.ComponentModel.DataAnnotations.Schema;

namespace Signal_Windows.Models
{
    public class SignalThread
    {
        public ulong Id { get; set; }
        public string ThreadId { get; set; }
        public string ThreadDisplayName { get; set; }
        public long LastActiveTimestamp { get; set; }
        public string LastMessage { get; set; }
        public string AvatarFile { get; set; }
        public uint Unread { get; set; }
        public bool CanReceive { get; set; }
        [NotMapped] public ThreadListItem View;
    }
}