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

namespace Signal_Windows
{
    /// <summary>
    /// Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
    /// </summary>
    sealed partial class App : Application
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<App>();
        public static string URL = "https://textsecure-service.whispersystems.org";
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };
        public static StorageFolder LocalCacheFolder = ApplicationData.Current.LocalCacheFolder;
        public static ViewModelLocator ViewModels = (ViewModelLocator)Current.Resources["Locator"];
        public static SignalStore Store;
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static SignalLibHandle Handle = new SignalLibHandle(false);
        Dictionary<int, CoreDispatcher> Views = new Dictionary<int, CoreDispatcher>();
        private int MainViewId;

        /// <summary>
        /// Initialisiert das Singletonanwendungsobjekt. Dies ist die erste Zeile von erstelltem Code
        /// und daher das logische Äquivalent von main() bzw. WinMain().
        /// </summary>
        public App()
        {
            SignalLogging.SetupLogging(true);
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
            this.Suspending += App_Suspending;
            this.Resuming += App_Resuming;
        }

        private async void App_Resuming(object sender, object e)
        {
            Logger.LogInformation("Resuming");
            await Task.Run(() =>
            {
                Handle.Acquire();
            });
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
            var frame = new StackTrace(e, true).GetFrames()[0];
            Logger.LogError("UnhandledException occured in {0}/{1}: {2}\n{3}", frame.GetFileName(), frame.GetFileLineNumber(), e.Message, e.StackTrace);
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
                Views.Add(currView.Id, Window.Current.Dispatcher);
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
                    await Task.Run(() =>
                    {
                        Handle.Acquire();
                    });
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    Window.Current.Activate();
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
                await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame frame = new Frame();
                    frame.Navigate(typeof(MainPage), e.Arguments);
                    Window.Current.Content = frame;
                    Window.Current.Activate();
                    var currView = ApplicationView.GetForCurrentView();
                    currView.Consolidated += CurrView_Consolidated;
                    newViewId = currView.Id;
                });
                Views.Add(newViewId, newView.Dispatcher);
                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId,
                        ViewSizePreference.Default,
                        e.CurrentlyShownApplicationViewId,
                        ViewSizePreference.Default);
            }
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }

        private void CurrView_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            sender.Consolidated -= CurrView_Consolidated;
            var dispatcher = Views[sender.Id];
            Views.Remove(sender.Id);
            Handle.RemoveWindow(dispatcher);
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