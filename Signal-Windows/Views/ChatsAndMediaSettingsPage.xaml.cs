using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Signal_Windows.Lib;
using Signal_Windows.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Signal_Windows.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChatsAndMediaSettingsPage : Page
    {
        public ChatsAndMediaSettingsPage()
        {
            this.InitializeComponent();
        }

        public ChatsAndMediaSettingsPageViewModel Vm
        {
            get { return (ChatsAndMediaSettingsPageViewModel)DataContext; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Utils.EnableBackButton(BackButton_Click);
            Vm.OnNavigatedTo();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            Utils.DisableBackButton(BackButton_Click);
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            Frame.GoBack();
            e.Handled = true;
        }

        private void SpellCheckToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            Vm.SpellCheckToggleSwitch_Toggled(toggleSwitch.IsOn);
        }
    }
}
