using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Signal_Windows.Models
{
    public abstract class SignalConversation
    {
        public long Id { get; set; }
        public string ThreadId { get; set; }
        public string ThreadDisplayName { get; set; }
        public long LastActiveTimestamp { get; set; }
        public string Draft { get; set; }
        public string AvatarFile { get; set; }
        public long MessagesCount { get; set; }
        public uint UnreadCount { get; set; }
        public bool CanReceive { get; set; }
        public uint ExpiresInSeconds { get; set; }
        public long? LastMessageId { get; set; }
        public SignalMessage LastMessage { get; set; }
        public long LastSeenMessageIndex { get; set; }
        public SignalMessage LastSeenMessage { get; set; }
        [NotMapped] public string DisplayedColor { get; set; }
        public Action UpdateUI;

        public SignalConversation Clone()
        {
            if (this is SignalContact contact)
            {
                return new SignalContact()
                {
                    Id = Id,
                    ThreadId = ThreadId,
                    ThreadDisplayName = ThreadDisplayName,
                    LastActiveTimestamp = LastActiveTimestamp,
                    Draft = Draft,
                    AvatarFile = AvatarFile,
                    MessagesCount = MessagesCount,
                    UnreadCount = UnreadCount,
                    CanReceive = CanReceive,
                    ExpiresInSeconds = ExpiresInSeconds,
                    LastMessageId = LastMessageId,
                    LastMessage = LastMessage,
                    LastSeenMessageIndex = LastSeenMessageIndex,
                    LastSeenMessage = LastSeenMessage,
                    Color = contact.Color
                };
            }
            else
            {
                return new SignalGroup()
                {
                    Id = Id,
                    ThreadId = ThreadId,
                    ThreadDisplayName = ThreadDisplayName,
                    LastActiveTimestamp = LastActiveTimestamp,
                    Draft = Draft,
                    AvatarFile = AvatarFile,
                    MessagesCount = MessagesCount,
                    UnreadCount = UnreadCount,
                    CanReceive = CanReceive,
                    ExpiresInSeconds = ExpiresInSeconds,
                    LastMessageId = LastMessageId,
                    LastMessage = LastMessage,
                    LastSeenMessageIndex = LastSeenMessageIndex,
                    LastSeenMessage = LastSeenMessage
                };
            }

        }
    }
}