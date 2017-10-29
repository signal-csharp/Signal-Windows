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
using Microsoft.QueryStringDotNET;
using Windows.UI.Notifications;
using Microsoft.Extensions.Logging;
using Windows.Foundation.Diagnostics;
using Windows.ApplicationModel.Background;
using Signal_Windows.RC;
using System.Threading;

namespace Signal_Windows
{
    /// <summary>
    /// Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
    /// </summary>
    sealed partial class App : Application
    {
        public static string URL = "https://textsecure-service.whispersystems.org";
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };
        public static StorageFolder LocalFolder = ApplicationData.Current.LocalFolder;
        public static ViewModelLocator ViewModels = (ViewModelLocator)Current.Resources["Locator"];
        public static SignalStore Store;
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static bool WindowActive = false;
        private Task<SignalStore> Init;
        public static bool BackgroundTaskRunning;
        public static Semaphore AppSemaphore;

        /// <summary>
        /// Initialisiert das Singletonanwendungsobjekt. Dies ist die erste Zeile von erstelltem Code
        /// und daher das logische Äquivalent von main() bzw. WinMain().
        /// </summary>
        public App()
        {
            LibsignalLogging.LoggerFactory.AddProvider(new SignalLoggerProvider());
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += App_Resuming;
            try
            {
                Init = Task.Run(() =>
                {
                    SignalDBContext.Migrate();
                    LibsignalDBContext.Migrate();
                    return LibsignalDBContext.GetSignalStore();
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn die Anwendung durch den Endbenutzer normal gestartet wird. Weitere Einstiegspunkte
        /// werden z. B. verwendet, wenn die Anwendung gestartet wird, um eine bestimmte Datei zu öffnen.
        /// </summary>
        /// <param name="e">Details über Startanforderung und -prozess.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await OnLaunchedOrActivated(e);
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await OnLaunchedOrActivated(args, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="launched">
        /// If OnLaunched this is true
        /// If OnActivated this is false
        /// </param>
        /// <returns></returns>
        private async Task OnLaunchedOrActivated(IActivatedEventArgs e, bool launched = true)
        {
            AttemptSemaphoreSetup();

            string TaskName = "SignalMessageBackgroundTask";
            BackgroundExecutionManager.RemoveAccess();
            bool foundTask = false;

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == TaskName)
                {
                    foundTask = true;
                }
            }

            if (!foundTask)
            {
                var builder = new BackgroundTaskBuilder();
                builder.Name = TaskName;
                builder.TaskEntryPoint = "Signal_Windows.RC.SignalBackgroundTask";
                builder.IsNetworkRequested = true;
                builder.SetTrigger(new TimeTrigger(15, false));
                builder.SetTrigger(new SystemTrigger(SystemTriggerType.ServicingComplete, false));
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                var requestStatus = await BackgroundExecutionManager.RequestAccessAsync();
                if (requestStatus != BackgroundAccessStatus.DeniedBySystemPolicy ||
                    requestStatus != BackgroundAccessStatus.DeniedByUser ||
                    requestStatus != BackgroundAccessStatus.Unspecified)
                {
                    builder.Register();
                }
            }

            Debug.WriteLine("Signal-Windows " + LocalFolder.Path.ToString());
            Window.Current.Activated += Current_Activated;
            WindowActive = true;
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
            }

            if (launched)
            {
                LaunchActivatedEventArgs args = e as LaunchActivatedEventArgs;
                if (args.PrelaunchActivated == false)
                {
                    if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                    {
                        var sb = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                        sb.BackgroundColor = Windows.UI.Color.FromArgb(1, 0x20, 0x90, 0xEA);
                        sb.BackgroundOpacity = 1;
                        sb.ForegroundColor = Windows.UI.Colors.White;
                    }

                    if (rootFrame.Content == null)
                    {
                        // Wenn der Navigationsstapel nicht wiederhergestellt wird, zur ersten Seite navigieren
                        // und die neue Seite konfigurieren, indem die erforderlichen Informationen als Navigationsparameter
                        // übergeben werden
                        Store = await Init;
                        if (Store == null || !Store.Registered)
                        {
                            rootFrame.Navigate(typeof(StartPage), args.Arguments);
                        }
                        else
                        {
                            rootFrame.Navigate(typeof(MainPage), args.Arguments);
                        }
                    }
                }
            }
            else
            {
                if (e is ToastNotificationActivatedEventArgs)
                {
                    var args = e as ToastNotificationActivatedEventArgs;
                    QueryString queryString = QueryString.Parse(args.Argument);
                    if (!(rootFrame.Content is MainPage))
                    {
                        rootFrame.Navigate(typeof(MainPage), queryString);
                    }
                }
            }
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
            // Sicherstellen, dass das aktuelle Fenster aktiv ist
            Window.Current.Activate();
        }

        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                WindowActive = false;
            }
            else
            {
                WindowActive = true;
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

        /// <summary>
        /// Wird aufgerufen, wenn die Ausführung der Anwendung angehalten wird.  Der Anwendungszustand wird gespeichert,
        /// ohne zu wissen, ob die Anwendung beendet oder fortgesetzt wird und die Speicherinhalte dabei
        /// unbeschädigt bleiben.
        /// </summary>
        /// <param name="sender">Die Quelle der Anhalteanforderung.</param>
        /// <param name="e">Details zur Anhalteanforderung.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            if (MainPageActive)
            {
                if (ViewModels.MainPageInstance != null)
                {
                    await ViewModels.MainPageInstance.Shutdown();
                }
            }
            if (AppSemaphore != null)
            {
                AppSemaphore.Release();
            }
            Debug.WriteLine("shutdown successful");
            //TODO: Anwendungszustand speichern und alle Hintergrundaktivitäten beenden
            deferral.Complete();
        }

        private async void App_Resuming(object sender, object e)
        {
            Debug.WriteLine("Resuming app");
            if (ViewModels.MainPageInstance != null)
            {
                await ViewModels.MainPageInstance.Init();
            }
            else
            {
                Debug.WriteLine("We can't resume");
            }
        }

        private void AttemptSemaphoreSetup()
        {
            AppSemaphore = null;
            BackgroundTaskRunning = true;
            try
            {
                AppSemaphore = Semaphore.OpenExisting("Signal_Windows_Semaphore");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                BackgroundTaskRunning = false;
            }

            if (!BackgroundTaskRunning)
            {
                AppSemaphore = new Semaphore(1, 1, "Signal_Windows_Semaphore");
                AppSemaphore.WaitOne();
            }
        }
    }
}
