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
        private bool loading = true;

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
            loading = true;
            base.OnNavigatedTo(e);
            Utils.EnableBackButton(Vm.BackButton_Click);
            Vm.OnNavigatedTo();
            SetSelectedDisappearingRadioButton(TimeSpan.FromSeconds(Vm.Contact.ExpiresInSeconds));
            loading = false;
        }

        void SetSelectedDisappearingRadioButton(TimeSpan expiresIn)
        {
            if (expiresIn == TimeSpan.Zero)
            {
                DisappearingOffButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromSeconds(5))
            {
                Disappearing5sButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromSeconds(10))
            {
                Disappearing10sButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromSeconds(30))
            {
                Disappearing30sButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromMinutes(1))
            {
                Disappearing1mButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromMinutes(5))
            {
                Disappearing5mButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromMinutes(30))
            {
                Disappearing30mButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromHours(1))
            {
                Disappearing1hButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromHours(6))
            {
                Disappearing6hButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromHours(12))
            {
                Disappearing12hButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromDays(1))
            {
                Disappearing1dButton.IsChecked = true;
            }
            else if (expiresIn == TimeSpan.FromDays(7))
            {
                Disappearing1wButton.IsChecked = true;
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(Vm.BackButton_Click);
            await Vm.OnNavigatingFrom();
        }

        private void Color00_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color01_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color02_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color03_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color04_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color05_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color06_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color07_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color08_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color09_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color10_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color11_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color12_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color13_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void Color14_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SolidColorBrush brush = button.Background as SolidColorBrush;
            Vm.SetContactColor(brush);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.ResetContactColor();
        }

        private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Vm.UpdateDisplayName(((TextBox)sender).Text);
        }

        private void BlockButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.BlockButton_Click();
        }

        private async void DisappearingOffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.Zero);
            }
        }

        private async void Disappearing5sButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromSeconds(5));
            }
        }

        private async void Disappearing10sButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromSeconds(10));
            }
        }

        private async void Disappearing30sButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromSeconds(30));
            }
        }

        private async void Disappearing1mButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromMinutes(1));
            }
        }

        private async void Disappearing5mButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromMinutes(5));
            }
        }

        private async void Disappearing30mButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromMinutes(30));
            }
        }

        private async void Disappearing1hButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromHours(1));
            }
        }

        private async void Disappearing6hButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromHours(6));
            }
        }

        private async void Disappearing12hButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromHours(12));
            }
        }

        private async void Disappearing1dButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromDays(1));
            }
        }

        private async void Disappearing1wButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!loading)
            {
                await Vm.SetDisappearingMessagesTime(TimeSpan.FromDays(7));
            }
        }
    }
}
