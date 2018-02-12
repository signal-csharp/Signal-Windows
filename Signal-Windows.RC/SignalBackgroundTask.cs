
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
        private ISignalLibHandle Handle;
        private ToastNotifier ToastNotifier;
        private AutoResetEvent ResetEvent = new AutoResetEvent(false);
        private EventWaitHandle GlobalResetEvent;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            
            Logger.LogInformation("Background task starting");
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
            GlobalResetEvent = LibUtils.OpenResetEventUnset();
            Task.Run(() =>
            {
                GlobalResetEvent.WaitOne();
                Logger.LogInformation("Background task received app startup signal");
                ResetEvent.Set();
            });
            try
            {
                Handle = SignalHelper.CreateSignalLibHandle(true);
                Handle.SignalMessageEvent += Handle_SignalMessageEvent;
                Handle.BackgroundAcquire();
                ResetEvent.WaitOne();
            }
            catch (Exception e)
            {
                Logger.LogError("Background task failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                Logger.LogInformation("Background task shutting down");
                Handle.BackgroundRelease();
                LibUtils.Unlock();
                Deferral.Complete();
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Logger.LogInformation("Background task received cancel request");
            ResetEvent.Set();
        }

        private void Handle_SignalMessageEvent(object sender, SignalMessageEventArgs e)
        {
            if (e.MessageType == Lib.Events.SignalMessageType.NormalMessage)
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
            else if (e.MessageType == Lib.Events.SignalMessageType.PipeEmptyMessage)
            {
                Logger.LogInformation("Background task has drained the pipe");
                ResetEvent.Set();
            }
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