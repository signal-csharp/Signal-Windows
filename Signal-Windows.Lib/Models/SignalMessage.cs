using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace Signal_Windows.Models
{
    public class SignalMessage
    {
        public long Id { get; set; }
        public SignalMessageDirection Direction { get; set; }
        public SignalMessageType Type { get; set; }
        public SignalMessageStatus Status { get; set; }
        public SignalMessageContent Content { get; set; }
        public string ThreadId { get; set; }
        public long? AuthorId { get; set; }
        public SignalContact Author { get; set; }
        public uint DeviceId { get; set; }
        public bool Read { get; set; }
        public uint Receipts { get; set; }
        public long ReceivedTimestamp { get; set; }
        public long ComposedTimestamp { get; set; }
        public long ExpiresAt { get; set; }
        public uint AttachmentsCount { get; set; }
        public List<SignalAttachment> Attachments { get; set; } = new List<SignalAttachment>();
    }

    public enum SignalMessageType
    {
        Normal = 0,
        GroupUpdate = 1,
        SessionReset = 2,
        ExpireUpdate = 3,
        IdentityKeyChange = 4,
        GroupLeave = 5
    }

    public enum SignalMessageDirection
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
        Failed_Network = 4,
        Failed_Ratelimit = 5,
        Failed_Unknown = 6
    }
}