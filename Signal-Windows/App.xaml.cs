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
using Windows.ApplicationModel.Background;

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
        private IBackgroundTaskRegistration backgroundTaskRegistration;

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

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            Logger.LogInformation("OnActivated() {0}", args.GetType());
            if (args is ToastNotificationActivatedEventArgs toastArgs)
            {
                bool createdMainWindow = await CreateMainWindow(toastArgs.Argument);
                if (!createdMainWindow)
                {
                    if (args is IViewSwitcherProvider viewSwitcherProvider && viewSwitcherProvider.ViewSwitcher != null)
                    {
                        ActivationViewSwitcher switcher = viewSwitcherProvider.ViewSwitcher;
                        int currentId = toastArgs.CurrentlyShownApplicationViewId;
                        if (viewSwitcherProvider.ViewSwitcher.IsViewPresentedOnActivationVirtualDesktop(toastArgs.CurrentlyShownApplicationViewId))
                        {
                            await Views[currentId].Dispatcher.RunTaskAsync(() =>
                            {
                                Logger.LogInformation("OnActivated() selecting conversation");
                                Views[currentId].Locator.MainPageInstance.SelectConversation(toastArgs.Argument);
                            });
                            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(currentId);
                        }
                        else
                        {
                            await CreateSecondaryWindow(switcher, toastArgs.Argument);
                        }
                    }
                    else
                    {
                        Logger.LogError("OnActivated() has no ViewSwitcher");
                    }
                }
            }
            else
            {
                Logger.LogError("unknown IActivatedEventArgs {0}", args);
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            string taskName = "SignalMessageBackgroundTask";
            bool foundTask = false;
            BackgroundExecutionManager.RemoveAccess();
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == taskName)
                {
                    backgroundTaskRegistration = task.Value;
                    foundTask = true;
                }
            }

            if (!foundTask)
            {
                var builder = new BackgroundTaskBuilder();
                builder.Name = taskName;
                builder.TaskEntryPoint = "Signal_Windows.RC.SignalBackgroundTask";
                builder.IsNetworkRequested = true;
                builder.SetTrigger(new TimeTrigger(15, false));
                builder.SetTrigger(new SystemTrigger(SystemTriggerType.ServicingComplete, false));
                builder.SetTrigger(new SystemTrigger(SystemTriggerType.TimeZoneChange, false));
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                var requestStatus = await BackgroundExecutionManager.RequestAccessAsync();
                if (requestStatus != BackgroundAccessStatus.DeniedBySystemPolicy ||
                    requestStatus != BackgroundAccessStatus.DeniedByUser ||
                    requestStatus != BackgroundAccessStatus.Unspecified)
                {
                    backgroundTaskRegistration = builder.Register();
                }
            }

            backgroundTaskRegistration.Completed += BackgroundTaskRegistration_Completed;

            Logger.LogInformation("Launching ({0})", e.PreviousExecutionState);
            Logger.LogDebug(LocalCacheFolder.Path);

            bool createdMainWindow = await CreateMainWindow(null);
            if (!createdMainWindow)
            {
                ActivationViewSwitcher switcher = e.ViewSwitcher;
                int currentId = e.CurrentlyShownApplicationViewId;
                if (!switcher.IsViewPresentedOnActivationVirtualDesktop(currentId))
                {
                    await CreateSecondaryWindow(e.ViewSwitcher, e.Arguments);
                }
                else
                {
                    await ApplicationViewSwitcher.TryShowAsStandaloneAsync(currentId);
                }
            }
        }

        private async Task CreateSecondaryWindow(ActivationViewSwitcher switcher, string conversationId)
        {
            Logger.LogInformation("CreateSecondaryWindow()");
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            SignalWindowsFrontend frontend = await newView.Dispatcher.RunTaskAsync(async () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(MainPage), conversationId);
                Window.Current.Content = frame;
                Window.Current.Activate();
                var currView = ApplicationView.GetForCurrentView();
                currView.Consolidated += CurrView_Consolidated;
                newViewId = currView.Id;
                await switcher.ShowAsStandaloneAsync(newViewId);
                ViewModelLocator newVML = (ViewModelLocator)Resources["Locator"];
                return new SignalWindowsFrontend(newView.Dispatcher, newVML, newViewId);
            });
            Views.Add(newViewId, frontend);
            await newView.Dispatcher.RunTaskAsync(() =>
            {
                Handle.AddFrontend(frontend.Dispatcher, frontend);
            });
            Logger.LogInformation("OnLaunched added view {0}", newViewId);
        }

        private async Task<bool> CreateMainWindow(string conversationId)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
                var currView = ApplicationView.GetForCurrentView();
                var frontend = new SignalWindowsFrontend(Window.Current.Dispatcher, (ViewModelLocator)Resources["Locator"], currView.Id);
                Views.Add(currView.Id, frontend);
                MainViewId = currView.Id;

                if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var sb = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                    sb.BackgroundColor = Windows.UI.Color.FromArgb(1, 0x20, 0x90, 0xEA);
                    sb.BackgroundOpacity = 1;
                    sb.ForegroundColor = Windows.UI.Colors.White;
                }

                try
                {
                    ApplicationViewSwitcher.DisableShowingMainViewOnActivation();
                    ApplicationViewSwitcher.DisableSystemViewActivationPolicy();
                    rootFrame.Navigate(typeof(MainPage), conversationId);
                    Window.Current.Activate();
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear();
                    // We need to await here so that any exception that Acquire throws actually gets caught
                    await Handle.Acquire(frontend.Dispatcher, frontend);
                }
                catch (Exception ex)
                {
                    var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("OnLaunchedOrActivated() could not load signal handle: {0}: {1}\n{2}", line, ex.Message, ex.StackTrace);
                    rootFrame.Navigate(typeof(StartPage));
                }
                return true;
            }
            return false;
        }

        private void BackgroundTaskRegistration_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            Debug.WriteLine("Background task completed");
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
