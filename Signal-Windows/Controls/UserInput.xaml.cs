using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Signal_Windows.Controls
{
    public sealed partial class UserInput : UserControl
    {
        public Brush SendButtonBackground => Utils.Blue;
        public event Action OnSendMessageButtonClicked;
        public event Action OnEnterKeyPressed;
        public event Action OnUnblockButtonClicked;
        public event Action OnInputTextBoxTextChanged;

        public UserInput()
        {
            this.InitializeComponent();
        }

        public string InputText
        {
            get => InputTextBox.Text;
            set => InputTextBox.Text = value;
        }

        public void AddLinefeed()
        {
            string prefix = InputTextBox.Text.Substring(0, InputTextBox.SelectionStart);
            string suffix = InputTextBox.Text.Substring(InputTextBox.SelectionStart);
            var pos = InputTextBox.SelectionStart;
            InputTextBox.Text = prefix + "\r" + suffix;
            InputTextBox.SelectionStart = pos + 1;
            InputTextBox.SelectionLength = 0;
        }

        public void SetSendButtonIcon(Symbol newIcon)
        {
            SendMessageButtonSymbol.Symbol = newIcon;
        }

        public void FocusTextBox()
        {
            InputTextBox.Focus(FocusState.Programmatic);
        }

        public bool SendButtonEnabled
        {
            get { return (bool)GetValue(SendButtonEnabledProperty); }
            set { SetValue(SendButtonEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SendButtonEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SendButtonEnabledProperty =
            DependencyProperty.Register("SendButtonEnabled", typeof(bool), typeof(UserInput), null);

        public Visibility BlockedVisibility
        {
            get { return (Visibility)GetValue(BlockedVisibilityProperty); }
            set { SetValue(BlockedVisibilityProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BlockedVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BlockedVisibilityProperty =
            DependencyProperty.Register("BlockedVisibility", typeof(Visibility), typeof(UserInput), null);

        public Visibility SendMessageVisibility
        {
            get { return (Visibility)GetValue(SendMessageVisibilityProperty); }
            set { SetValue(SendMessageVisibilityProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SendMessageVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SendMessageVisibilityProperty =
            DependencyProperty.Register("SendMessageVisibility", typeof(Visibility), typeof(UserInput), null);

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            OnSendMessageButtonClicked?.Invoke();
        }

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true; // Prevent KeyDown from firing twice on W10 CU
                OnEnterKeyPressed?.Invoke();
            }
        }

        private void UnblockButton_Click(object sender, RoutedEventArgs e)
        {
            OnUnblockButtonClicked?.Invoke();
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnInputTextBoxTextChanged?.Invoke();
        }
    }
}
