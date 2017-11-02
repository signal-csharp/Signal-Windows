using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace Signal_Windows.RC
{
    public sealed class SignalBackgroundTask : IBackgroundTask
    {
        private const string TaskName = "SignalMessageBackgroundTask";
        private const string SemaphoreName = "Signal_Windows_Semaphore";

        private BackgroundTaskDeferral deferral;
        private bool TaskCanceled { get; set; }

        private Semaphore semaphore;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.Canceled += TaskInstance_Canceled;
            deferral = taskInstance.GetDeferral();
            bool appRunning = IsAppRunning();
            if (appRunning)
            {
                deferral.Complete();
                return;
            }
            semaphore.Release();
            deferral.Complete();
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            TaskCanceled = true;
        }

        private bool IsAppRunning()
        {
            semaphore = null;
            try
            {
                semaphore = Semaphore.OpenExisting("Signal_Windows_Semaphore");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return true;
            }

            semaphore = new Semaphore(1, 1, "Signal_Windows_Semaphore");
            semaphore.WaitOne();
            return false;
        }
    }
}
