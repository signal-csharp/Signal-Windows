using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static void AddMessage(SignalMessage message)
        {
            if (message.ExpiresAt == 0)
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
                    Debug.WriteLine($"deleteTimeSpan was less than 0: {deleteTimeSpan.ToString()}");
                    Debug.WriteLine($"Deleting message: {message.Content.Content}");
                    await DeleteMessage(message);
                    return;
                }
                Debug.WriteLine($"Deleting message in {deleteTimeSpan.TotalSeconds} seconds");
                await Task.Delay(deleteTimeSpan);
                Debug.WriteLine($"Deleting message: {message.Content.Content}");
                await DeleteMessage(message);
            };
            Task.Run(deleteTask);
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

            foreach (var attachment in message.Attachments)
            {
                SignalDBContext.DeleteAttachment(attachment);
            }
            SignalDBContext.DeleteMessage(message);
        }
    }
}
