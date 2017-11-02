using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Lib.Events
{
    public class IdentityKeyChangeEventArgs : EventArgs
    {
        public string Number { get; private set; }

        public IdentityKeyChangeEventArgs(string number)
        {
            Number = number;
        }
    }
}
