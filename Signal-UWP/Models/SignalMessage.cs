using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_UWP.Models
{
    public class SignalMessage
    {
        public uint Id { get; set; }
        public uint Type { get; set; }
        public string Content { get; set; }
        public string ThreadID { get; set; }
    }
}
