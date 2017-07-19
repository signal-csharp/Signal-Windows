using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Models
{
    public class SignalEarlyReceipt
    {
        public uint Id { get; set; }
        public string Username { get; set; }
        public uint DeviceId { get; set; }
        public long Timestamp { get; set; }
    }
}
