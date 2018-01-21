using libsignalservice.push;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Signal_Windows.Lib
{
    public static class DispatcherTaskExtensions
    {
        // Taken from https://github.com/Microsoft/Windows-task-snippets/blob/master/tasks/UI-thread-task-await-from-background-thread.md
        public static async Task<T> RunTaskAsync<T>(this CoreDispatcher dispatcher,
            Func<Task<T>> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            await dispatcher.RunAsync(priority, async () =>
            {
                try
                {
                    taskCompletionSource.SetResult(await func());
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });
            return await taskCompletionSource.Task;
        }

        // There is no TaskCompletionSource<void> so we use a bool that we throw away.
        public static async Task RunTaskAsync(this CoreDispatcher dispatcher,
            Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal) =>
            await RunTaskAsync(dispatcher, async () => { await func(); return false; }, priority);

        // We want to await
        public static async Task<bool> RunTaskAsync(this CoreDispatcher dispatcher,
            Action func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            await dispatcher.RunAsync(priority, () =>
            {
                try
                {
                    func();
                    taskCompletionSource.SetResult(false);
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });
            return await taskCompletionSource.Task;
        }
    }

    public class LibUtils
    {
        public const string GlobalSemaphoreName = "SignalWindowsPrivateMessenger_Mutex";
        public static string URL = "https://textsecure-service.whispersystems.org";
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl(URL, null) };
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static bool WindowActive = false;
        public static Semaphore GlobalSemaphore;

        internal static void Lock()
        {
            GlobalSemaphore = new Semaphore(1, 1, GlobalSemaphoreName, out bool b);
            GlobalSemaphore.WaitOne();
        }

        internal static void Unlock()
        {
            GlobalSemaphore.Release();
        }
    }
}
