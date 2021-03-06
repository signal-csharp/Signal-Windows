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
            Vm.View = this;
        }

        public AddContactPageViewModel Vm
        {
            get
            {
                return (AddContactPageViewModel)DataContext;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs ev)
        {
            base.OnNavigatedTo(ev);
            Utils.EnableBackButton(Vm.BackButton_Click);
            await Vm.OnNavigatedTo();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(Vm.BackButton_Click);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await Vm.AddButton_Click(sender, e);
        }

        private async void ContactsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            await Vm.ContactsList_ItemClick(sender, e);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            Vm.SearchBox_TextChanged(sender, args);
        }

        private void ContactNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Vm.ContactNameTextBox_TextChanged(sender, e);
        }

        private void ContactNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Vm.ContactNumberTextBox_TextChanged(sender, e);
        }

        private async void ContactsList_RefreshRequested(object sender, System.EventArgs e)
        {
            await Vm.RefreshContacts();
        }
    }
}