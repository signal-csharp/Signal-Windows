using GalaSoft.MvvmLight;
using Signal_Windows.Models;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Signal_Windows.Storage;
using Windows.UI.Core;

namespace Signal_Windows.ViewModels
{
    public class ConversationSettingsPageViewModel : ViewModelBase
    {
        public ConversationSettingsPage View;

        private string _Initials = string.Empty;
        public string Initials
        {
            get { return _Initials; }
            set { _Initials = value; RaisePropertyChanged(nameof(Initials)); }
        }

        private SolidColorBrush _FillBrush;
        public SolidColorBrush FillBrush
        {
            get { return _FillBrush; }
            set { _FillBrush = value; RaisePropertyChanged(nameof(FillBrush)); }
        }

        private SolidColorBrush _AccentColor;
        public SolidColorBrush AccentColor
        {
            get { return _AccentColor; }
            set { _AccentColor = value; RaisePropertyChanged(nameof(AccentColor)); }
        }

        private string _DisplayName;
        public string DisplayName
        {
            get { return _DisplayName; }
            set { _DisplayName = value; RaisePropertyChanged(nameof(DisplayName)); }
        }
        private string oldDisplayName;

        public ObservableCollection<SolidColorBrush> Colors { get; set; }
        public SignalContact Contact { get; set; }

        private bool blocked;
        public bool Blocked
        {
            get { return blocked; }
            set
            {
                blocked = value;
                RaisePropertyChanged(nameof(Blocked));
                if (blocked)
                {
                    BlockedText = "Unblock";
                }
                else
                {
                    BlockedText = "Block";
                }
            }
        }

        private string blockedText;
        public string BlockedText
        {
            get { return blockedText; }
            set { blockedText = value; RaisePropertyChanged(nameof(BlockedText)); }
        }

        public ConversationSettingsPageViewModel()
        {
            Colors = new ObservableCollection<SolidColorBrush>();
            foreach (var color in Utils.Colors)
            {
                Colors.Add(Utils.GetBrushFromColor(color));
            }
            Colors.Add(Utils.Grey);
            AccentColor = new SolidColorBrush((Color)App.Current.Resources["SystemAccentColor"]);
        }

        public void OnNavigatedTo()
        {
            Blocked = Contact.Blocked;
            FillBrush = Utils.GetBrushFromColor(Contact.Color);
            Initials = Utils.GetInitials(Contact.ThreadDisplayName);
            DisplayName = Contact.ThreadDisplayName;
            oldDisplayName = DisplayName;
        }

        internal async Task OnNavigatingFrom()
        {
            Contact.ThreadDisplayName = DisplayName.Trim();
            await Task.Run(() =>
            {
                App.Handle.SaveAndDispatchSignalConversation(Contact, null);
            });
        }

        internal void UpdateDisplayName(string newDisplayName)
        {
            DisplayName = newDisplayName;
            Initials = Utils.GetInitials(DisplayName);
        }

        internal void SetContactColor(SolidColorBrush brush)
        {
            Contact.Color = Utils.GetColorFromBrush(brush);
            FillBrush = Utils.GetBrushFromColor(Contact.Color);
        }

        internal void ResetContactColor()
        {
            Contact.Color = null;
            FillBrush = Utils.GetBrushFromColor(Utils.CalculateDefaultColor(Contact.ThreadDisplayName));
        }

        internal async Task SetDisappearingMessagesTime(TimeSpan timeSpan)
        {
            Contact.ExpiresInSeconds = (uint)timeSpan.TotalSeconds;
            await Task.Run(() =>
            {
                SignalDBContext.UpdateExpiresInLocked(Contact);
            });
        }

        internal void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            View.Frame.GoBack();
            e.Handled = true;
        }

        internal async void BlockButton_Click()
        {
            Contact.Blocked = !Contact.Blocked;
            Blocked = Contact.Blocked;
            SignalDBContext.UpdateBlockStatus(Contact);
            await Task.Run(() =>
            {
                App.Handle.SendBlockedMessage();
            });
        }
    }
}
