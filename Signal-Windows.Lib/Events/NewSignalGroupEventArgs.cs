using Signal_Windows.Lib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib.Events
{
    public class NewSignalGroupEventArgs : EventArgs
    {
        public SignalGroup Group { get; private set; }

        public NewSignalGroupEventArgs(SignalGroup group)
        {
            Group = group;
        }
    }
}
