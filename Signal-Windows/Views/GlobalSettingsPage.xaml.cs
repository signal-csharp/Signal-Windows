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
    public sealed partial class GlobalSettingsPage : Page
    {
        public GlobalSettingsPage()
        {
            this.InitializeComponent();
            Vm.View = this;
        }

        public GlobalSettingsPageViewModel Vm
        {
            get
            {
                return (GlobalSettingsPageViewModel)DataContext;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs ev)
        {
            base.OnNavigatedTo(ev);
            Utils.EnableBackButton(Vm.BackButton_Click);
            Vm.OnNavigatedTo();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Utils.DisableBackButton(Vm.BackButton_Click);
        }

        private async void ExportUIDebugLog(object sender, RoutedEventArgs e)
        {
            await Vm.ExportUIDebugLog();
        }

        private void RequestSync(object sender, RoutedEventArgs e)
        {
            App.Handle.RequestSync();
        }
    }
}
