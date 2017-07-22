using Signal_Windows.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class ThreadView : UserControl
    {
        public ThreadView()
        {
            this.InitializeComponent();
        }

        public ThreadViewModel Vm
        {
            get
            {
                return (ThreadViewModel)DataContext;
            }
        }

        public void ScrollToBottm()
        {
            SelectedMessagesScrollViewer.UpdateLayout();
            SelectedMessagesScrollViewer.ChangeView(0.0f, double.MaxValue, 1.0f);
        }

        private async void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            await Vm.MainPageVm.TextBox_KeyDown(sender, e);
        }
    }
}