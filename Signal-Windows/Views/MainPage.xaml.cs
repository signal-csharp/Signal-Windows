using Signal_Windows.ViewModels;
using Signal_Windows.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace Signal_Windows
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Vm.View = this;
        }

        public MainPageViewModel Vm
        {
            get
            {
                return (MainPageViewModel)DataContext;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AddContactPage));
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            Vm.TextBox_KeyDown(sender, e);
        }

        private void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UnselectedBlock.Visibility = Visibility.Collapsed;
            SelectedMessagesList.Visibility = Visibility.Visible;
            Vm.ContactsList_SelectionChanged(sender, e);
        }

        public void ScrollToBottom()
        {
            SelectedMessagesScrollViewer.UpdateLayout();
            SelectedMessagesScrollViewer.ChangeView(0.0f, double.MaxValue, 1.0f);
        }

        private void AddFriendSymbol_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(AddContactPage));
        }
    }
}