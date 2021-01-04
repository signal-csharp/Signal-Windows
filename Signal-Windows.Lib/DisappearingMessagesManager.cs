using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Windows.UI.Core;

namespace Signal_Windows.Lib
{
    public static class DisappearingMessagesManager
    {
        private static Dictionary<CoreDispatcher, ISignalFrontend> frames;

        static DisappearingMessagesManager()
        {
            frames = new Dictionary<CoreDispatcher, ISignalFrontend>();
        }

        public static void AddFrontend(CoreDispatcher coreDispatcher, ISignalFrontend frontend)
        {
            if (!frames.ContainsKey(coreDispatcher))
            {
                frames.Add(coreDispatcher, frontend);
            }
        }

        public static void RemoveFrontend(CoreDispatcher coreDispatcher)
        {
            if (frames.ContainsKey(coreDispatcher))
            {
                frames.Remove(coreDispatcher);
            }
        }

        /// <summary>
        /// Queues a message for deletion.
        /// </summary>
        /// <param name="message">The message to queue for deletion</param>
        /// <remarks>If the message expire time is 0 then the message will not be deleted.</remarks>
        public static void QueueForDeletion(SignalMessage message)
        {
            if (message.ExpiresAt <= 0)
            {
                return;
            }
            Action deleteTask = async () =>
            {
                DateTimeOffset expireTime = DateTimeOffset.FromUnixTimeMilliseconds(message.ExpiresAt);
                DateTimeOffset receivedTime = DateTimeOffset.FromUnixTimeMilliseconds(message.ReceivedTimestamp);
                TimeSpan deleteTimeSpan = expireTime - receivedTime;
                if (deleteTimeSpan < TimeSpan.Zero)
                {
                    await DeleteMessage(message);
                    return;
                }
                await Task.Delay(deleteTimeSpan);
                await DeleteMessage(message);
            };
            Task.Run(deleteTask);
        }

        /// <summary>
        /// Deletes expired messages from the database.
        /// </summary>
        public static void DeleteExpiredMessages()
        {
            long currentTimeMillis = Util.CurrentTimeMillis();
            List<SignalMessage> expiredMessages = SignalDBContext.GetExpiredMessages(currentTimeMillis);
            foreach (var expiredMessage in expiredMessages)
            {
                DeleteFromDb(expiredMessage);
            }
        }

        private static async Task DeleteMessage(SignalMessage message)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        frames[dispatcher].HandleMessageDelete(message);
                    }
                    catch (Exception e)
                    {
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }

            foreach (var t in operations)
            {
                await t;
            }

            DeleteFromDb(message);
        }

        private static void DeleteFromDb(SignalMessage message)
        {
            foreach (var attachment in message.Attachments)
            {
                SignalDBContext.DeleteAttachment(attachment);
            }
            SignalDBContext.DeleteMessage(message);
        }
    }
}
