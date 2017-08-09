using GalaSoft.MvvmLight;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;
using libsignalservice.util;
using Windows.UI.Popups;
using PhoneNumbers;

namespace Signal_Windows.ViewModels
{
    public class RegisterPageViewModel : ViewModelBase
    {
        public CancellationTokenSource CancelSource = new CancellationTokenSource();
        public List<string> CountriesList { get; set; } = new List<string>(250);
        private List<int> CountriesPrefixList { get; set; } = new List<int>(250);
        public RegisterPage View { get; set; }
        public string FinalNumber { get; set; }
        private string _PhonePrefix { get; set; }
        public string PhonePrefix
        {
            get
            {
                return _PhonePrefix;
            }
            set
            {
                _PhonePrefix = value;
                RaisePropertyChanged(nameof(PhonePrefix));
            }
        }

        public RegisterPageViewModel()
        {
            HashSet<string> set = PhoneNumberUtil.GetInstance().GetSupportedRegions();
            var phoneUtil = PhoneNumberUtil.GetInstance();
            foreach (var region in set)
            {
                if(region != "AC" && region != "SX" && region != "CW" && region != "BQ" && region != "TA" && region != "SS")
                {
                    string s = new Locale("en", region).GetDisplayCountry("en");
                    int prefix = phoneUtil.GetCountryCodeForRegion(region);
                    CountriesList.Add(s);
                    CountriesPrefixList.Add(prefix);
                }
            }
        }

        internal void RegisterPage_Loaded()
        {
            var c = Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
            for (int i=0;i<CountriesPrefixList.Count;i++)
            {
                int prefix = CountriesPrefixList[i];
                if (prefix == PhoneNumberUtil.GetInstance().GetCountryCodeForRegion(c))
                {
                    View.SetCountry(i);
                }
            }
        }

        private string _PhoneSuffix { get; set; }
        public string PhoneSuffix
        {
            get
            {
                return _PhoneSuffix;
            }
            set
            {
                _PhoneSuffix = value;
                RaisePropertyChanged(nameof(PhoneSuffix));
            }
        }

        public void RegisterButton_Click()
        {
            try
            {
                string number = PhoneNumberFormatter.FormatE164(PhonePrefix, PhoneSuffix);
                if(number != null && number.Length > 0 && number[0] == '+')
                {
                    FinalNumber = number;
                    View.Frame.Navigate(typeof(RegisterFinalizationPage));
                }
                else
                {
                    var title = "Invalid Phone Number";
                    var content = "The phone number you supplied appears to be invalid. Please enter your correct phone number and try again.";
                    MessageDialog dialog = new MessageDialog(content, title);
                    var result = dialog.ShowAsync();
                }
                Debug.WriteLine(number);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        public void OnNavigatingFrom()
        {
            Utils.DisableBackButton(BackButton_Click);
        }

        public void OnNavigatedTo()
        {
            Utils.EnableBackButton(BackButton_Click);
        }

        private void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            View.Frame.GoBack();
        }

        public void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox dropdown = (ComboBox)sender;
            string region = (string)dropdown.SelectedItem;
            int prefix = CountriesPrefixList[dropdown.SelectedIndex];
            PhonePrefix = "+" + prefix;
        }
    }
}