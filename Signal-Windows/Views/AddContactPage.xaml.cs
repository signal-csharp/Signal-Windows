using Signal_Windows.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace Signal_Windows.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class AddContactPage : Page
    {
        public AddContactPage()
        {
            this.InitializeComponent();
        }

        public AddContactPageViewModel Vm
        {
            get
            {
                return (AddContactPageViewModel)DataContext;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // probably not the best way to do this
            Vm.ContactPhoto = null;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.AddButton_Click(sender, e);
            Frame.Navigate(typeof(MainPage));
        }

        private void PickButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.PickButton_Click(sender, e);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Should add a back button using Windows.UI.Core.SystemNavigationManager
            // https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Core.SystemNavigationManager
            Frame.GoBack();
        }
    }
}