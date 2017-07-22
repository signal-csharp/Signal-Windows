using Signal_Windows.Controls;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Signal_Windows.Models
{
    public class SignalMessage
    {
        public ulong Id { get; set; }
        public SignalMessageType Type { get; set; }
        public SignalMessageStatus Status { get; set; }
        public SignalMessageContent Content { get; set; }
        public string ThreadId { get; set; }
        public ulong? AuthorId { get; set; }
        public SignalContact Author { get; set; }
        public uint DeviceId { get; set; }
        public uint Receipts { get; set; }
        public long ReceivedTimestamp { get; set; }
        public long ComposedTimestamp { get; set; }
        public uint ExpiresAt { get; set; }
        public uint AttachmentsCount { get; set; }
        public List<SignalAttachment> Attachments { get; set; }
        [NotMapped] public MessageBox View { get; set; }
    }

    public enum SignalMessageType
    {
        Outgoing = 0,
        Incoming = 1,
        Synced = 2
    }

    public enum SignalMessageStatus
    {
        Pending = 0,
        Confirmed = 1,
        Received = 2,
        Failed_Identity = 3,
        Failed_Network = 4
    }
}