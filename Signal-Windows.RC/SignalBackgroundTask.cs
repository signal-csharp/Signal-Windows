using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Signal_Windows.Lib;
using Signal_Windows.Lib.Events;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace Signal_Windows.RC
{
    public sealed class SignalBackgroundTask : IBackgroundTask
    {
        private const string TaskName = "SignalMessageBackgroundTask";
        private const string SemaphoreName = "Signal_Windows_Semaphore";

        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalBackgroundTask>();

        private BackgroundTaskDeferral deferral;

        private Semaphore semaphore;
        private DateTime taskStartTime;
        private DateTime taskEndTime;
        private ISignalLibHandle handle;
        private ToastNotifier toastNotifier;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            taskStartTime = DateTime.Now;
            taskEndTime = taskStartTime + TimeSpan.FromSeconds(25);
            taskInstance.Canceled += TaskInstance_Canceled;
            deferral = taskInstance.GetDeferral();
            SignalLogging.SetupLogging(false);
            toastNotifier = ToastNotificationManager.CreateToastNotifier();
            Logger.LogInformation("Background task starting");
            bool appRunning = IsAppRunning();
            if (appRunning)
            {
                Logger.LogWarning("App is running, background task shutting down");
                deferral.Complete();
                return;
            }
            handle = SignalHelper.CreateSignalLibHandle(true);
            handle.SignalMessageEvent += Handle_SignalMessageEvent;
            handle.BackgroundAcquire();
            await CheckTimer();
            Shutdown();
            deferral.Complete();
        }

        private async Task CheckTimer()
        {
            Logger.LogInformation("Started listening for messages");
            while (true)
            {
                if (DateTime.Now >= taskEndTime)
                {
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Logger.LogError($"Background task cancelled: {reason}");
            Shutdown();
            deferral.Complete();
        }

        private bool IsAppRunning()
        {
            semaphore = null;
            try
            {
                semaphore = Semaphore.OpenExisting(LibUtils.GlobalSemaphoreName);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                semaphore = new Semaphore(1, 1, LibUtils.GlobalSemaphoreName);
            }

            bool gotSignal = semaphore.WaitOne(TimeSpan.FromSeconds(5));
            return !gotSignal;
        }

        private void Shutdown()
        {
            Logger.LogInformation("Background task shutting down");
            handle.BackgroundRelease();
            semaphore.Release();
        }

        private void Handle_SignalMessageEvent(object sender, SignalMessageEventArgs e)
        {
            string notificationId = e.Message.ThreadId;
            ToastBindingGeneric toastBinding = new ToastBindingGeneric();

            var notificationText = GetNotificationText(e.Message.Author.ThreadDisplayName, e.Message.Content.Content);
            foreach (var item in notificationText)
            {
                toastBinding.Children.Add(item);
            }

            ToastContent toastContent = new ToastContent()
            {
                Launch = notificationId,
                Visual = new ToastVisual()
                {
                    BindingGeneric = toastBinding
                },
                DisplayTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Message.ReceivedTimestamp)
            };

            ToastNotification toastNotification = new ToastNotification(toastContent.GetXml());
            uint expiresIn = e.Message.ExpiresAt;
            if (expiresIn > 0)
            {
                toastNotification.ExpirationTime = DateTime.Now.Add(TimeSpan.FromSeconds(expiresIn));
            }
            toastNotification.Tag = notificationId;
            toastNotifier.Show(toastNotification);
        }

        private IList<AdaptiveText> GetNotificationText(string authorName, string content)
        {
            List<AdaptiveText> text = new List<AdaptiveText>();
            AdaptiveText title = new AdaptiveText()
            {
                Text = authorName,
                HintMaxLines = 1
            };
            AdaptiveText messageText = new AdaptiveText()
            {
                Text = content,
                HintWrap = true
            };
            text.Add(title);
            text.Add(messageText);
            return text;
        }
    }
}
