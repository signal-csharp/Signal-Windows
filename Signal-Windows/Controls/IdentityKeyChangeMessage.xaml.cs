using Signal_Windows.Lib;
using Signal_Windows.Models;
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
    public sealed partial class IdentityKeyChangeMessage : UserControl, IMessageView
    {
        public IdentityKeyChangeMessage(SignalMessage model)
        {
            this.InitializeComponent();
            this.DataContextChanged += IdentityKeyChangeMessage_DataContextChanged;
            Model = model;
        }

        public SignalMessage Model
        {
            get
            {
                return this.DataContext as SignalMessage;
            }
            set
            {
                this.DataContext = value;
            }
        }

        public void HandleUpdate(SignalMessage m)
        {
            throw new NotImplementedException();
        }

        private void IdentityKeyChangeMessage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                MessageTextBlock.Text = Model.Content.Content;
            }
            else
            {
                MessageTextBlock.Text = "null";
            }
        }

        public FrameworkElement AsFrameworkElement()
        {
            return this;
        }
    }
}
