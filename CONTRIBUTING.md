# Contributing

## Building

### Requirements

1. Windows 10 v1703 build 15063 (Creators Update) or greater
2. Visual Studio 2017

### Steps

1. Clone/fork the repository
2. Open the Signal-Windows.sln in Visual Studio
3. Build the solution
4. Deploy to your target device

## Testing Changes on Signal

### Using Two Devices

With this method it's recommended that you have another device with Signal installed so that you can send messages between your debugging client, typically your desktop, and your other device. It's also recommended that you set up your debugging client to use a [Google Voice](https://www.google.com/voice) number instead of your main phone number. Setup your other device, typically your phone, to use your main phone number. This way you can send messages between your two devices and you don't need to worry about losing important messages on your debugging client.

### Using signal-cli

If you only have your desktop you can use [signal-cli](https://github.com/AsamK/signal-cli) to send messages to your debugging client. You still will need another number to register signal-cli with.

## Backing Up the Database

If you want to backup your database files, `Libsignal.db` and `Signal.db`, you can find them in `C:\Users\<USERNAME>\AppData\Local\Packages\2383BenediktRadtke.SignalPrivateMessenger_teak1p7hcx9ga\LocalCache\`  

On mobile these are found at `LocalAppData\2383BenediktRadtke.SignalPrivateMessenger_<VERSION>_arm__teak1p7hcx9ga\LocalCache\`
