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
    public sealed partial class NotificationSettingsPage : Page
    {
        public NotificationSettingsPage()
        {
            this.InitializeComponent();
        }

        public NotificationSettingsPageViewModel Vm
        {
            get
            {
                return (NotificationSettingsPageViewModel)DataContext;
            }
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

        private void ShowNotificationText_Checked(object sender, RoutedEventArgs e)
        {
            var radioButton = sender as RadioButton;
            string tag = radioButton.Tag.ToString();
            if (tag == Vm.NameAndMessageTag)
            {
                if (GlobalSettingsManager.ShowNotificationTextSetting != GlobalSettingsManager.ShowNotificationTextSettings.NameAndMessage)
                {
                    GlobalSettingsManager.ShowNotificationTextSetting = GlobalSettingsManager.ShowNotificationTextSettings.NameAndMessage;
                    Vm.NameAndMessageChecked = true;
                }
            }
            else if (tag == Vm.NameOnlyTag)
            {
                if (GlobalSettingsManager.ShowNotificationTextSetting != GlobalSettingsManager.ShowNotificationTextSettings.NameOnly)
                {
                    GlobalSettingsManager.ShowNotificationTextSetting = GlobalSettingsManager.ShowNotificationTextSettings.NameOnly;
                    Vm.NameOnlyChecked = true;
                }
            }
            else if (tag == Vm.NoNameOrMessageTag)
            {
                if (GlobalSettingsManager.ShowNotificationTextSetting != GlobalSettingsManager.ShowNotificationTextSettings.NoNameOrMessage)
                {
                    GlobalSettingsManager.ShowNotificationTextSetting = GlobalSettingsManager.ShowNotificationTextSettings.NoNameOrMessage;
                    Vm.NoNameOrMessageChecked = true;
                }
            }
        }
    }
}
