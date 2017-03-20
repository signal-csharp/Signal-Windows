using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Signal_Windows.ViewModels
{
    public class SignalMessageStyleSelector : StyleSelector
    {
        public Style OutgoingMessageStyle { get; set; }
        public Style IncomingGreenMessageStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if(item.GetType() == typeof(SignalMessage))
            {
                SignalMessage msg = (SignalMessage)item;
                if (msg.Type == 0)
                {
                    return OutgoingMessageStyle;
                }
                else
                {
                    return IncomingGreenMessageStyle;
                }
            }
            else
            {
                Debug.WriteLine("SelectStyleCore for unknown container " + container);
            }
            return OutgoingMessageStyle;
        }
    }
}
