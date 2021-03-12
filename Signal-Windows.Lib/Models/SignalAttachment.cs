using System.ComponentModel.DataAnnotations.Schema;
using libsignalservice.messages;
using libsignalservice.util;
using Windows.UI.Xaml.Controls;

namespace Signal_Windows.Models
{
    // Database model
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
        public int CdnNumber { get; set; }

        /// <summary>
        /// The Signal attachment pointer V2 remote id.
        /// </summary>
        public ulong StorageId { get; set; }

        /// <summary>
        /// The Signal attachment pointer V3 remote id.
        /// </summary>
        public string V3StorageId { get; set; }
        public byte[] Digest { get; set; }
        public long Size { get; set; }
        public string Guid { get; set; }

        [NotMapped]
        public Image AttachmentImage { get; set; }

        public SignalServiceAttachmentPointer ToAttachmentPointer()
        {
            if (StorageId != 0)
            {
                return new SignalServiceAttachmentPointer(CdnNumber,
                    new SignalServiceAttachmentRemoteId((long)StorageId),
                    ContentType,
                    Key,
                    (uint)Util.ToIntExact(Size),
                    null,
                    0,
                    0,
                    Digest,
                    FileName,
                    false,
                    null,
                    null,
                    Util.CurrentTimeMillis());
            }
            else
            {
                return new SignalServiceAttachmentPointer(CdnNumber,
                    new SignalServiceAttachmentRemoteId(V3StorageId),
                    ContentType,
                    Key,
                    (uint)Util.ToIntExact(Size),
                    null,
                    0,
                    0,
                    Digest,
                    FileName,
                    false,
                    null,
                    null,
                    Util.CurrentTimeMillis());
            }
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