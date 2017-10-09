using System;
using Signal_Windows.ViewModels;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace Signal_Windows.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class StartPage : Page
    {
        private const string ContinueId = "continue";

        public StartPage()
        {
            this.InitializeComponent();
        }

        public StartPageViewModel Vm
        {
            get
            {
                return (StartPageViewModel)DataContext;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Utils.DisableBackButton();
            var view = ApplicationView.GetForCurrentView();
            view.TitleBar.BackgroundColor = Utils.Default.Color;
            view.TitleBar.ButtonBackgroundColor = Utils.Default.Color;
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var registerDialog = new MessageDialog("Registering a new Signal account will terminate any previously " +
                "registered accounts associated with your phone number and unlink all slave devices.");
            registerDialog.Commands.Add(new UICommand("Continue", new UICommandInvokedHandler(RegisterDialogHandler), ContinueId));
            registerDialog.Commands.Add(new UICommand("Cancel"));
            registerDialog.DefaultCommandIndex = 1;
            registerDialog.CancelCommandIndex = 1;
            await registerDialog.ShowAsync();
        }

        private void RegisterDialogHandler(IUICommand command)
        {
            if ((string)command.Id == ContinueId)
            {
                Frame.Navigate(typeof(RegisterPage));
            }
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LinkPage));
        }
    }
}