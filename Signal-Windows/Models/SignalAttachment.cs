using System.ComponentModel.DataAnnotations.Schema;
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

        [NotMapped]
        public Image AttachmentImage { get; set; }
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