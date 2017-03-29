using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
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

        public ObservableCollection<SignalMessage> MessagesSource
        {
            get { return (ObservableCollection<SignalMessage>)GetValue(MessagesSourceProperty); }
            set { SetValue(MessagesSourceProperty, value); }
        }

        public static readonly DependencyProperty MessagesSourceProperty =
            DependencyProperty.Register("MessagesSource", typeof(ObservableCollection<SignalMessage>), typeof(ThreadView), new PropertyMetadata(null));

        public Visibility WelcomeVisibility
        {
            get { return (Visibility)GetValue(WelcomeVisibilityProperty); }
            set { SetValue(WelcomeVisibilityProperty, value); }
        }

        public static readonly DependencyProperty WelcomeVisibilityProperty =
            DependencyProperty.Register("WelcomeVisibility", typeof(Visibility), typeof(ThreadView), new PropertyMetadata(Visibility.Visible));

        public Visibility MainVisibility
        {
            get { return (Visibility)GetValue(MainVisibilityProperty); }
            set { SetValue(MainVisibilityProperty, value); }
        }

        public static readonly DependencyProperty MainVisibilityProperty =
            DependencyProperty.Register("MainVisibility", typeof(Visibility), typeof(ThreadView), new PropertyMetadata(Visibility.Collapsed));

        public MainPageViewModel MainPageVM
        {
            get { return (MainPageViewModel)GetValue(MainPageVMProperty); }
            set { SetValue(MainPageVMProperty, value); }
        }

        public static readonly DependencyProperty MainPageVMProperty =
            DependencyProperty.Register("MainPageVM", typeof(MainPageViewModel), typeof(ThreadView), new PropertyMetadata(null));

        public string ThreadTitle
        {
            get { return (string)GetValue(ThreadTitleProperty); }
            set { SetValue(ThreadTitleProperty, value); }
        }

        public static readonly DependencyProperty ThreadTitleProperty =
            DependencyProperty.Register("ThreadTitle", typeof(string), typeof(ThreadView), new PropertyMetadata("default"));

        public void ScrollToBottm()
        {
            SelectedMessagesScrollViewer.UpdateLayout();
            SelectedMessagesScrollViewer.ChangeView(0.0f, double.MaxValue, 1.0f);
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            MainPageVM.TextBox_KeyDown(sender, e);
        }
    }
}