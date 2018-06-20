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
    public sealed partial class AddedAttachment : UserControl
    {
        public event Action OnCancelAttachmentButtonClicked;
        public AddedAttachment()
        {
            this.InitializeComponent();
        }

        public void ShowAttachment(string filename)
        {
            Visibility = Visibility.Visible;
            AddedAttachmentFilename.Text = filename;
        }

        public void HideAttachment()
        {
            Visibility = Visibility.Collapsed;
        }

        private void CancelAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            OnCancelAttachmentButtonClicked?.Invoke();
        }
    }
}
