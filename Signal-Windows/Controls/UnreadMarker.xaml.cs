using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class UnreadMarker : UserControl
    {
        public UnreadMarker()
        {
            this.InitializeComponent();
            DataContextChanged += UnreadMarker_DataContextChanged;
        }

        public SignalUnreadMarker Model
        {
            get
            {
                return this.DataContext as SignalUnreadMarker;
            }
        }

        private void UnreadMarker_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Model.View = this;
        }

        public void SetText(string text)
        {
            UnreadText.Text = text;
        }
    }
}
