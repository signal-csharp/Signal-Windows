using System.ComponentModel.DataAnnotations.Schema;
using Windows.UI.Xaml.Controls;

namespace Signal_Windows.Models
{
    public class SignalAttachment
    {
        public uint Id { get; set; }
        public SignalMessage Message { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public uint Status { get; set; }
        public byte[] Key { get; set; }
        public string Relay { get; set; }
        public ulong StorageId { get; set; }

        [NotMapped]
        public Image AttachmentImage { get; set; }
    }

    public enum SignalAttachmentStatus
    {
        Default = 0,
        Finished = 1
    }
}