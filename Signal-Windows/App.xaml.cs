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
using Windows.Networking.BackgroundTransfer;
using Windows.UI;
using libsignalservice.configuration;

namespace Signal_Windows
{
    /// <summary>
    /// Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
    /// </summary>
    sealed partial class App : Application
    {
        private static App Instance;
        private static ILogger Logger = LibsignalLogging.CreateLogger<App>();
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl("https://textsecure-service.whispersystems.org") };
        public static SignalServiceConfiguration ServiceConfiguration = new SignalServiceConfiguration(ServiceUrls, null);
        public static StorageFolder LocalCacheFolder = ApplicationData.Current.LocalCacheFolder;
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static ISignalLibHandle Handle = SignalHelper.CreateSignalLibHandle(false);
        private Dictionary<int, SignalWindowsFrontend> Views = new Dictionary<int, SignalWindowsFrontend>();
        public static int MainViewId;
        private IBackgroundTaskRegistration backgroundTaskRegistration;

        static App()
        {
            // TODO enforce these have begun before initializing and ensure the logger is working
            Task.Run(() => { SignalDBContext.Migrate(); });
            Task.Run(() => { LibsignalDBContext.Migrate(); });
        }
        /// <summary>
        /// Initialisiert das Singletonanwendungsobjekt. Dies ist die erste Zeile von erstelltem Code
        /// und daher das logische Äquivalent von main() bzw. WinMain().
        /// </summary>
        public App()
        {
            SignalFileLoggerProvider.ForceAddUILog(Utils.GetAppStartMessage());
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
                string requestedConversation = toastArgs.Argument;
                bool createdMainWindow = await CreateMainWindow(requestedConversation);
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
                                Views[currentId].Locator.MainPageInstance.TrySelectConversation(requestedConversation);
                            });
                            await viewSwitcherProvider.ViewSwitcher.ShowAsStandaloneAsync(currentId);
                        }
                        else
                        {
                            await CreateSecondaryWindowOrShowMain(switcher, requestedConversation);
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
            Logger.LogInformation("Launching (PreviousExecutionState={0})", e.PreviousExecutionState);
            try
            {
                string taskName = "SignalMessageBackgroundTask";
                foreach (var task in BackgroundTaskRegistration.AllTasks)
                {
                    if (task.Value.Name == taskName)
                    {
                        task.Value.Unregister(false);
                    }
                }

                var builder = new BackgroundTaskBuilder();
                builder.Name = taskName;
                builder.TaskEntryPoint = "Signal_Windows.RC.SignalBackgroundTask";
                builder.IsNetworkRequested = true;
                builder.SetTrigger(new TimeTrigger(15, false));
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                var requestStatus = await BackgroundExecutionManager.RequestAccessAsync();
                if (requestStatus != BackgroundAccessStatus.DeniedBySystemPolicy ||
                    requestStatus != BackgroundAccessStatus.DeniedByUser ||
                    requestStatus != BackgroundAccessStatus.Unspecified)
                {
                    backgroundTaskRegistration = builder.Register();
                }
                else
                {
                    Logger.LogWarning($"Unable to register background task: {requestStatus}");
                }

                backgroundTaskRegistration.Completed += BackgroundTaskRegistration_Completed;
            }
            catch(Exception ex)
            {
                Logger.LogError("Cannot setup bg task: {0}\n{1}", ex.Message, ex.StackTrace);
            }

            bool createdMainWindow = await CreateMainWindow(null);
            if (!createdMainWindow)
            {
                ActivationViewSwitcher switcher = e.ViewSwitcher;
                int currentId = e.CurrentlyShownApplicationViewId;
                if (!switcher.IsViewPresentedOnActivationVirtualDesktop(currentId))
                {
                    await CreateSecondaryWindowOrShowMain(e.ViewSwitcher, e.Arguments);
                }
                else
                {
                    await switcher.ShowAsStandaloneAsync(currentId);
                }
            }
        }

        private async Task CreateSecondaryWindowOrShowMain(ActivationViewSwitcher switcher, string conversationId)
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
                //await switcher.ShowAsStandaloneAsync(newViewId);
                ViewModelLocator newVML = (ViewModelLocator)Resources["Locator"];
                SetupTopBar();
                return new SignalWindowsFrontend(newView.Dispatcher, newVML, newViewId);
            });
            bool success = await newView.Dispatcher.RunTaskAsync(async () =>
            {
                //AddFrontend blocks for the handle lock, but the new window is not yet registered, so nothing will be invoked
                return Handle.AddFrontend(frontend.Dispatcher, frontend);
            });
            if (success)
            {
                Views.Add(newViewId, frontend);
                await newView.Dispatcher.RunTaskAsync(async () =>
                {
                    await switcher.ShowAsStandaloneAsync(newViewId);
                });
                Logger.LogInformation("CreateSecondaryWindow() added view {0}", newViewId);
            }
            else
            {
                Logger.LogInformation("CreateSecondaryWindow() showing MainView {0}", MainViewId);
                await switcher.ShowAsStandaloneAsync(MainViewId);
            }
        }

        private void SetupTopBar()
        {
            // mobile clients have a status bar
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var sb = StatusBar.GetForCurrentView();
                sb.BackgroundColor = Color.FromArgb(1, 0x20, 0x90, 0xEA);
                sb.BackgroundOpacity = 1;
                sb.ForegroundColor = Colors.White;
            }
            // desktop clients have a title bar
            else if(Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    titleBar.ButtonBackgroundColor = Color.FromArgb(1, 0x20, 0x90, 0xEA);
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.BackgroundColor = Color.FromArgb(1, 0x20, 0x90, 0xEA);
                    titleBar.ForegroundColor = Colors.White;
                }
                else
                {
                    Logger.LogError("TitleBar is null");
                }
            }
            else
            {
                Logger.LogError("Neither TitleBar nor StatusBar found");
            }
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
                SetupTopBar();

                try
                {
                    ApplicationViewSwitcher.DisableShowingMainViewOnActivation();
                    ApplicationViewSwitcher.DisableSystemViewActivationPolicy();
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear();
                    // We need to await here so that any exception that Acquire throws actually gets caught
                    var hasStoreRecord = await Handle.Acquire(frontend.Dispatcher, frontend);
                    if (hasStoreRecord)
                    {
                        rootFrame.Navigate(typeof(MainPage), conversationId);
                    }
                    else
                    {
                        Handle.Release();
                        rootFrame.Navigate(typeof(StartPage));
                    }
                    Window.Current.Activate();
                }
                catch (Exception ex)
                {
                    var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("OnLaunchedOrActivated() could not load signal handle: {0}\n{1}", ex.Message, ex.StackTrace);
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
