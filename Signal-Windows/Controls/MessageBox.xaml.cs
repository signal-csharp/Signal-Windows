using Signal_Windows.Models;
using System;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class MessageBox : UserControl
    {
        public MessageBox()
        {
            this.InitializeComponent();
            this.DataContextChanged += MessageBox_DataContextChanged;
        }

        private void MessageBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Debug.WriteLine("MessageBox_DataContextChanged " + sender); //name opacity 204
            if (Model.Author == null)
            {
                Background = GetSolidColorBrush(255, "#f3f3f3");
                Foreground = GetSolidColorBrush(255, "#454545");
                HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                Foreground = GetSolidColorBrush(255, "#ffffff");
                Background = GetSolidColorBrush(255, Model.Author.Color);
                HorizontalAlignment = HorizontalAlignment.Left;
            }
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

        public SolidColorBrush GetSolidColorBrush(byte opacity, string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte r = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(opacity, r, g, b));
            return myBrush;
        }
    }
}