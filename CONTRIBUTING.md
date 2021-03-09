# Contributing

See more documentation in the `docs` folder.

## Building

### Requirements

1. Windows 10 v1703 build 15063 (Creators Update) or greater
2. Visual Studio 2017 15.4 RC or greater

### Steps

1. Clone/fork the repository
2. Open the Signal-Windows.sln in Visual Studio
3. Build the solution
4. Deploy to your target device

## Testing Changes on Signal

Signal-Windows is able to exchange messages with every other signal device. You may also send messages to yourself, they will be sent to all sibling devices. Use any signal client you like, and setup your deployed Signal-Windows instance:

### Using Signal-Windows as a master device

Note: Signal-Windows master devices cannot yet link slaves.
You can register Signal-Windows as a master on any supported W10 device using a virtual (e.g. [Google Voice](https://www.google.com/voice)), mobile or landline phone number.

### Using Signal-Windows as a slave device
You can link Signal-Windows as a slave to any signal device capable of linking slaves.

#### Using a Signal-Android or Signal-iOS master
Same procedure as with Signal-Desktop: Scan the qr-code.

#### Using a [signal-cli](https://github.com/AsamK/signal-cli) master
Signal-Windows kindly also displays the tsdevice string below the qrcode. Use `signal-cli addDevice --uri` like you would with a Signal-Desktop slave.

## Backing Up the Database
#### Beware: Only backup Libsignal.db if you know what you are doing. The Signal Protocol is stateful, so replacing the database with an older version will most likely corrupt existing sessions with your contacts.

If you want to backup your database files, `Libsignal.db` and `Signal.db`, you can find them in `C:\Users\<USERNAME>\AppData\Local\Packages\2383BenediktRadtke.SignalPrivateMessenger_teak1p7hcx9ga\LocalCache\`  

On mobile these are found at `LocalAppData\2383BenediktRadtke.SignalPrivateMessenger_<VERSION>_arm__teak1p7hcx9ga\LocalCache\`

## Adding a New View

1. Add a new Blank Page in `Signal-Windows/Views`. For example NewPage.xaml.
2. Add a new class in `Signal-Windows/ViewModels`. The name of the class should be the `ViewModel` appended to the page name. For example NewPageViewModel.cs.
3. Inherit from `ViewModelBase` (namespace `GalaSoft.MvvmLight`) in the new view model class. For example `public class NewPageViewModel : ViewModelBase`
4. Add a property for the new page in the new view model class. For example `public NewView View { get; set; }`
5. In the new view code behind add a property for the new view model class. For example `public NewPageViewModel Vm { get { return (NewPageViewModel)DataContext; } }`
6. In the new view code behind assign the view model `View` to the code behind class in the constructor. For example in the NewPage constructor add `Vm.View = this;` after `this.InitializeComponent();`
7. In `Signal-Windows/ViewModels/ViewModelLocator.cs` register the new page view model in `SimpleIoc`. For example `SimpleIoc.Default.Register<NewPageViewModel>();`
8. Add a property that returns an instance of the new page view model. For example `public NewPageViewModel NewPageInstance { get { return ServiceLocator.Current.GetInstance<NewPageViewModel>(Key.ToString()); } }`
9. In the new page XAML set the DataContext of the page to the instance of the new page view model. This must be set in the Page opening tag. For example `DataContext="{Binding NewPageInstance, Source={StaticResource Locator}}"`
