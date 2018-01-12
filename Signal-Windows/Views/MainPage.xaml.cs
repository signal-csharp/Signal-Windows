using Signal_Windows.Controls;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using Signal_Windows.Views;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

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
            Vm.LoadConversations(SignalLibHandle.Instance.AddWindow(Window.Current.Dispatcher, Vm));
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Frame.SizeChanged -= Frame_SizeChanged;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Frame.SizeChanged += Frame_SizeChanged;
            SwitchToStyle(GetCurrentViewStyle());
            MainPanel.DisplayMode = SplitViewDisplayMode.CompactInline;
        }

        public void SwitchToStyle(PageStyle newStyle)
        {
            if (newStyle == PageStyle.Narrow)
            {
                if (Vm.SelectedThread != null)
                {
                    Utils.EnableBackButton(Vm.BackButton_Click);
                    MainPanel.IsPaneOpen = false;
                    MainPanel.CompactPaneLength = 0;
                }
                else
                {
                    Unselect();
                    MainPanel.IsPaneOpen = true;
                }
            }
            else if (newStyle == PageStyle.Wide)
            {
                Utils.DisableBackButton(Vm.BackButton_Click);
                MainPanel.IsPaneOpen = false;
                MainPanel.CompactPaneLength = ContactsGrid.Width = 320;
            }
            UpdateStyle(newStyle);
        }

        private void UpdateStyle(PageStyle currentStyle)
        {
            if (currentStyle == PageStyle.Narrow)
            {
                // TODO: When phone is in landscape mode this is incorrect and some stuff gets cut off, we need to
                // get the actual useable width (actualwidth - top icon bar - bottom control bar)
                ContactsGrid.Width = ActualWidth;
                if (Vm.SelectedThread == null)
                {
                    MainPanel.OpenPaneLength = ActualWidth;
                }
            }
            else if (currentStyle == PageStyle.Wide)
            {
                MainPanel.CompactPaneLength = MainPanel.OpenPaneLength = ContactsGrid.Width = 320;
            }
        }

        public PageStyle GetCurrentViewStyle()
        {
            return Utils.GetViewStyle(new Size(ActualWidth, ActualHeight));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            ContactsList.SelectedItem = null;
        }

        private void Frame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var oldStyle = Utils.GetViewStyle(e.PreviousSize);
            var newStyle = Utils.GetViewStyle(e.NewSize);
            if (oldStyle != newStyle)
            {
                SwitchToStyle(newStyle);
            }
            UpdateStyle(newStyle);
        }

        public MainPageViewModel Vm
        {
            get
            {
                return (MainPageViewModel)DataContext;
            }
        }

        public Conversation Thread
        {
            get
            {
                return ConversationControl;
            }
        }

        private void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && Vm.SelectedThread != e.AddedItems[0])
            {
                Vm.WelcomeVisibility = Visibility.Collapsed;
                Vm.ThreadVisibility = Visibility.Visible;
                Vm.SelectedThread = (SignalConversation)e.AddedItems[0];
                ConversationControl.Load(Vm.SelectedThread);
                SwitchToStyle(GetCurrentViewStyle());
            }
        }

        public static async Task NotifyNewIdentity(string user)
        {
            var title = "Safety Numbers Change";
            var content = "Your safety numbers with " + user + " have changed. This happens when someone is attempting to intercept your communication, or when your contact reinstalled signal on a different device.";
            UICommand understood = new UICommand("I understand");
            MessageDialog dialog = new MessageDialog(content, title);
            dialog.Commands.Add(understood);
            dialog.DefaultCommandIndex = 0;
            var result = await dialog.ShowAsync();
        }

        public void Unselect()
        {
            ContactsList.SelectedItem = null;
        }

        public void ReselectTop()
        {
            ContactsList.SelectedIndex = 0;
        }

        private void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModelLocator.CurrentVML.AddContactPageInstance.MainPageVM = Vm;
            //App.ViewModels.AddContactPageInstance.ContactName = "";
            //App.ViewModels.AddContactPageInstance.ContactNumber = "";
            //Frame.Navigate(typeof(AddContactPage)); TODO
        }
    }
}