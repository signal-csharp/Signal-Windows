using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Models
{
    public class SignalMessage
    {
        public uint Id { get; set; }
        public uint Type { get; set; }
        public string Content { get; set; }
        public string ThreadID { get; set; }
        public SignalContact Author { get; set; }
        //public uint DeviceId { get; set; }
        //public uint Receipts { get; set; }
        public long ReceivedTimestamp { get; set; }
        public long ComposedTimestamp { get; set; }
    }
}
