using System.ComponentModel.DataAnnotations.Schema;
using libsignalservice.messages;
using libsignalservice.util;
using Windows.UI.Xaml.Controls;

namespace Signal_Windows.Models
{
    public class SignalAttachment
    {
        public long Id { get; set; }
        public long MessageId { get; set; }
        public SignalMessage Message { get; set; }
        public string FileName { get; set; }
        public string SentFileName { get; set; }
        public string ContentType { get; set; }
        public SignalAttachmentStatus Status { get; set; }
        public byte[] Key { get; set; }
        public string Relay { get; set; }
        public ulong StorageId { get; set; }
        public byte[] Digest { get; set; }
        public long Size { get; set; }
        public string Guid { get; set; }

        [NotMapped]
        public Image AttachmentImage { get; set; }

        public SignalServiceAttachmentPointer ToAttachmentPointer()
        {
            return new SignalServiceAttachmentPointer(StorageId,
                ContentType,
                Key,
                Relay,
                (uint)Util.toIntExact(Size),
                null,
                Digest,
                FileName,
                false);
        }
    }

    public enum SignalAttachmentStatus
    {
        Default = 0,
        Finished = 1,
        InProgress = 2,
        Failed = 3,
        Failed_Permanently = 4
    }
}