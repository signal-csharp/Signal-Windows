using Signal_Windows.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace Signal_Windows.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            this.InitializeComponent();
            Vm.View = this;
        }

        public RegisterPageViewModel Vm
        {
            get
            {
                return (RegisterPageViewModel)DataContext;
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.RegisterButton_Click(sender, e);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.ConfirmButton_Click(sender, e);
        }

        public void NavigateForward()
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}