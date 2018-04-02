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
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalBackgroundTask>();
        private BackgroundTaskDeferral Deferral;
        private DateTime TaskStartTime;
        private DateTime TaskEndTime;
        private SignalLibHandle Handle;
        private ToastNotifier ToastNotifier;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Logger.LogInformation("Background task starting");
            TaskStartTime = DateTime.Now;
            TaskEndTime = TaskStartTime + TimeSpan.FromSeconds(25);
            Deferral = taskInstance.GetDeferral();
            SignalLogging.SetupLogging(false);
            ToastNotifier = ToastNotificationManager.CreateToastNotifier();
            taskInstance.Canceled += OnCanceled;
            bool locked = LibUtils.Lock(5000);
            Logger.LogTrace("Locking global finished, locked = {0}", locked);
            if (!locked)
            {
                Logger.LogWarning("App is running, background task shutting down");
                Deferral.Complete();
                return;
            }
            try
            {
                Handle = new SignalLibHandle(true);
                Handle.SignalMessageEvent += Handle_SignalMessageEvent;
                Handle.BackgroundAcquire();
                await CheckTimer();
            }
            catch (Exception e)
            {
                Logger.LogError("Background task failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                LibUtils.Unlock();
                Deferral.Complete();
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Logger.LogWarning("Background task received cancel request");
            try
            {
                Handle.BackgroundRelease();
            }
            catch(Exception e)
            {
                Logger.LogError("OnCanceled() failed : {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                LibUtils.Unlock();
                Logger.LogWarning("Background task cancel handler finished");
            }
        }

        private async Task CheckTimer()
        {
            Logger.LogInformation("Started listening for messages");
            while (true)
            {
                if (DateTime.Now >= TaskEndTime)
                {
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
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
            ToastNotifier.Show(toastNotification);
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
