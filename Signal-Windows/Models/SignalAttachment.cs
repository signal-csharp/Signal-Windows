using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
    public enum SignalAttachmentStatus
    {
        Default = 0,
        Finished = 1
    }
}
