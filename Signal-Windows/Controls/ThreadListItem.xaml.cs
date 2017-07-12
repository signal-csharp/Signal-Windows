using Signal_Windows.Models;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class ThreadListItem : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public SignalThread Model
        {
            get
            {
                return DataContext as SignalThread;
            }
            set
            {
                DataContext = value;
            }
        }

        private string _DisplayName;

        public string DisplayName
        {
            get
            {
                return _DisplayName;
            }
            set
            {
                _DisplayName = value;
            }
        }

        public ThreadListItem()
        {
            this.InitializeComponent();
            this.DataContextChanged += ThreadListItem_DataContextChanged;
        }

        private void ThreadListItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (Model != null)
            {
                Model.View = this;
                DisplayName = Model.ThreadDisplayName;
                Reload();
            }
        }

        public void Reload()
        {
            DisplayName = Model.ThreadDisplayName;
            PropertyChanged(this, new PropertyChangedEventArgs("DisplayName"));
        }
    }
}
