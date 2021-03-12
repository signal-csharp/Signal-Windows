using libsignalservice;
using Microsoft.Extensions.Logging;
using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib.Models
{
    // Not a database model
    public class SignalAttachmentContainer
    {
        public SignalAttachment Attachment;
        public int AttachmentIndex;
        public int MessageIndex;
        public SignalAttachmentContainer(SignalAttachment attachment, int attachmentIndex, int messageIndex)
        {
            Attachment = attachment;
            AttachmentIndex = attachmentIndex;
            MessageIndex = messageIndex;
        }
    }
}
