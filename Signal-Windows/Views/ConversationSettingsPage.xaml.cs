using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class ConversationSettingsPage : Page
    {
        public ConversationSettingsPageViewModel Vm
        {
            get { return (ConversationSettingsPageViewModel)DataContext; }
        }

        public ConversationSettingsPage()
        {
            this.InitializeComponent();
            Vm.View = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Utils.EnableBackButton(Vm.BackButton_Click);
            Vm.OnNavigatedTo();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(Vm.BackButton_Click);
            await Vm.OnNavigatingFrom();
        }

        private async void SystemColor_Click(object sender, RoutedEventArgs e)
        {
            await Vm.SetContactColor("system");
        }

        private async void Color00_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color01_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color02_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color03_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color04_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color05_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color06_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color07_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color08_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color09_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color10_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color11_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color12_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color13_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color14_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void Color15_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            await Vm.SetContactColor(brush);
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            await Vm.ResetContactColor();
        }

        private void DisplayNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Vm.UpdateDisplayName(((TextBox)sender).Text);
        }
    }
}
