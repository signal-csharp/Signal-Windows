using Signal_Windows.ViewModels;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Popups;
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
        }

        private MainPageStyle GetViewStyle(Size s)
        {
            if(s.Width <= 640)
            {
                return MainPageStyle.Narrow;
            }
            else
            {
                return MainPageStyle.Wide;
            }
        }

        private void Frame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var oldStyle = GetViewStyle(e.PreviousSize);
            var newStyle = GetViewStyle(e.NewSize);
            if(oldStyle != newStyle)
            {
                Debug.WriteLine("new style "+newStyle);
                if (newStyle == MainPageStyle.Narrow)
                {
                    if (Vm.SelectedThread != null)
                    {
                        Vm.ThreadListVisibility = Visibility.Collapsed;
                        Vm.ThreadVisibility = Visibility.Visible;
                    }
                    else
                    {
                        Vm.ThreadListVisibility = Visibility.Visible;
                        Vm.ThreadVisibility = Visibility.Collapsed;
                    }
                }
                else if (newStyle == MainPageStyle.Wide)
                {
                    Vm.ThreadListVisibility = Visibility.Visible;
                    Vm.ThreadVisibility = Visibility.Visible;
                }
            }
        }

        public MainPageViewModel Vm
        {
            get
            {
                return (MainPageViewModel)DataContext;
            }
        }

        private void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Vm.ContactsList_SelectionChanged(sender, e);
        }

        public void ScrollToBottom()
        {
            ThreadView.ScrollToBottm();
        }

        private void AddFriendSymbol_Tapped(object sender, TappedRoutedEventArgs e)
        {
            App.ViewModels.AddContactPageInstance.MainPageVM = Vm;
            App.ViewModels.AddContactPageInstance.ContactName = "";
            App.ViewModels.AddContactPageInstance.ContactNumber = "";
            Frame.Navigate(typeof(AddContactPage));
        }

        public static async Task NotifyNewIdentity(string user)
        {
            var title = "Identity Key Change";
            var content = "The identity key of " + user + " has changed. This happens when someone is attempting to intercept your communication, or when your contact re-registered on a different device.";
            UICommand understood = new UICommand("I understand");
            MessageDialog dialog = new MessageDialog(content, title);
            dialog.Commands.Add(understood);
            dialog.DefaultCommandIndex = 0;
            var result = await dialog.ShowAsync();
        }
    }

    public enum MainPageStyle
    {
        Narrow,
        Wide
    }
}