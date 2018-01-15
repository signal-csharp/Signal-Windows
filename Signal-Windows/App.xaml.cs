using libsignalservice.push;
using libsignalservice;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.ViewModels;
using Signal_Windows.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Notifications;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Signal_Windows
{
    /// <summary>
    /// Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
    /// </summary>
    sealed partial class App : Application
    {
        private static App Instance;
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<App>();
        public static string URL = "https://textsecure-service.whispersystems.org";
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };
        public static StorageFolder LocalCacheFolder = ApplicationData.Current.LocalCacheFolder;
        public static SignalStore Store;
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static SignalLibHandle Handle = new SignalLibHandle(false);
        private Dictionary<int, SignalWindowsFrontend> Views = new Dictionary<int, SignalWindowsFrontend>();
        public static int MainViewId;

        static App()
        {
            // TODO enforce these have begun before initializing
            Task.Run(() => { SignalDBContext.Migrate(); });
            Task.Run(() => { LibsignalDBContext.Migrate(); });
        }
        /// <summary>
        /// Initialisiert das Singletonanwendungsobjekt. Dies ist die erste Zeile von erstelltem Code
        /// und daher das logische Äquivalent von main() bzw. WinMain().
        /// </summary>
        public App()
        {
            Instance = this;
            SignalLogging.SetupLogging(true);
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
            this.Suspending += App_Suspending;
            this.Resuming += App_Resuming;
        }

        public static SignalWindowsFrontend CurrentSignalWindowsFrontend(int id)
        {
            return Instance.Views[id];
        }

        private async void App_Resuming(object sender, object e)
        {
            Logger.LogInformation("Resuming");
            await Handle.Reacquire();
        }

        private async void App_Suspending(object sender, SuspendingEventArgs e)
        {
            Logger.LogInformation("Suspending");
            var def = e.SuspendingOperation.GetDeferral();
            await Task.Run(() => Handle.Release());
            def.Complete();
            Logger.LogDebug("Suspended");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs ex)
        {
            Exception e = ex.Exception;
            Logger.LogError("UnhandledException {0} occured ({1}):\n{2}", e.GetType(), e.Message, e.StackTrace);
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Logger.LogInformation("OnActivated() {0}", args);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Logger.LogInformation("Launching ({0})", e.PreviousExecutionState);
            Logger.LogDebug(LocalCacheFolder.Path);
            Frame rootFrame = Window.Current.Content as Frame;

            // App-Initialisierung nicht wiederholen, wenn das Fenster bereits Inhalte enthält.
            // Nur sicherstellen, dass das Fenster aktiv ist.
            if (rootFrame == null)
            {
                // Frame erstellen, der als Navigationskontext fungiert und zum Parameter der ersten Seite navigieren
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Zustand von zuvor angehaltener Anwendung laden
                }

                // Den Frame im aktuellen Fenster platzieren
                Window.Current.Content = rootFrame;
                var currView = ApplicationView.GetForCurrentView();
                var frontend = new SignalWindowsFrontend(Window.Current.Dispatcher, (ViewModelLocator)Resources["Locator"], currView.Id);
                Views.Add(currView.Id, frontend);
                MainViewId = currView.Id;
            }

            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var sb = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                sb.BackgroundColor = Windows.UI.Color.FromArgb(1, 0x20, 0x90, 0xEA);
                sb.BackgroundOpacity = 1;
                sb.ForegroundColor = Windows.UI.Colors.White;
            }

            if (rootFrame.Content == null)
            {
                // Creating the rootFrame for the first time
                try
                {
                    ApplicationViewSwitcher.DisableShowingMainViewOnActivation();
                    ApplicationViewSwitcher.DisableSystemViewActivationPolicy();
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    Window.Current.Activate();
                    var frontend = CurrentSignalWindowsFrontend(MainViewId);
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear();
                    var acquisition = Handle.Acquire(frontend.Dispatcher, frontend);
                }
                catch (Exception ex)
                {
                    var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("OnLaunchedOrActivated() could not load signal handle: {0}: {1}\n{2}", line, ex.Message, ex.StackTrace);
                    rootFrame.Navigate(typeof(StartPage), e.Arguments);
                }
            }
            else
            {
                // user has requested a new window
                CoreApplicationView newView = CoreApplication.CreateNewView();
                int newViewId = 0;
                SignalWindowsFrontend frontend = await newView.Dispatcher.RunTaskAsync(async () =>
                {
                    Frame frame = new Frame();
                    frame.Navigate(typeof(MainPage), e.Arguments);
                    Window.Current.Content = frame;
                    Window.Current.Activate();
                    var currView = ApplicationView.GetForCurrentView();
                    currView.Consolidated += CurrView_Consolidated;
                    newViewId = currView.Id;
                    await e.ViewSwitcher.ShowAsStandaloneAsync(newViewId);
                    ViewModelLocator newVML = (ViewModelLocator)Resources["Locator"];
                    return new SignalWindowsFrontend(newView.Dispatcher, newVML, newViewId);
                });
                Views.Add(newViewId, frontend);
                await newView.Dispatcher.RunTaskAsync(async () =>
                {
                    Handle.AddFrontend(frontend.Dispatcher, frontend);
                });
                Logger.LogInformation("OnLaunched added view {0}", newViewId);
            }
        }

        private void CurrView_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            sender.Consolidated -= CurrView_Consolidated;
            var signalWindowsFrontend = Views[sender.Id];
            Handle.RemoveFrontend(signalWindowsFrontend.Dispatcher);
            Views.Remove(sender.Id);
            if (sender.Id != MainViewId)
            {
                Window.Current.Close();
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn die Navigation auf eine bestimmte Seite fehlschlägt
        /// </summary>
        /// <param name="sender">Der Rahmen, bei dem die Navigation fehlgeschlagen ist</param>
        /// <param name="e">Details über den Navigationsfehler</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}