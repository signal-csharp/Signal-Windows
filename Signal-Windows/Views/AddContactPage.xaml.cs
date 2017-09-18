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
            // probably not the best way to do this
            //Vm.ContactPhoto = null;
            await Vm.OnNavigatedTo();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(Vm.BackButton_Click);
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

        private void ContactsList_ItemClick(object sender, ItemClickEventArgs e)
        {

        }
    }
}