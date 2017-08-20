using Signal_Windows.Controls;
using System.ComponentModel.DataAnnotations.Schema;

namespace Signal_Windows.Models
{
    public class SignalConversation
    {
        public long Id { get; set; }
        public string ThreadId { get; set; }
        public string ThreadDisplayName { get; set; }
        public long LastActiveTimestamp { get; set; }
        public string Draft { get; set; }
        public string AvatarFile { get; set; }
        public uint UnreadCount { get; set; }
        public bool CanReceive { get; set; }
        public uint ExpiresInSeconds { get; set; }
        public long? LastMessageId { get; set; }
        public SignalMessage LastMessage { get; set; }
        public long? LastSeenMessageId { get; set; }
        public SignalMessage LastSeenMessage { get; set; }
        [NotMapped] public ConversationListElement View;
    }
}