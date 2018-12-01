using libsignalservice;
using libsignalservice.configuration;
using libsignalservice.push;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Notifications;

namespace Signal_Windows.Lib
{
    public class LibUtils
    {
        private static readonly ILogger Logger = LibsignalLogging.CreateLogger<LibUtils>();
        public const string GlobalMutexName = "SignalWindowsPrivateMessenger_Mutex";
        public const string GlobalEventWaitHandleName = "SignalWindowsPrivateMessenger_EventWaitHandle";
        public static string URL = "https://textsecure-service.whispersystems.org";
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl("https://textsecure-service.whispersystems.org") };
        public static SignalServiceConfiguration ServiceConfiguration = new SignalServiceConfiguration(ServiceUrls, null);
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static bool WindowActive = false;
        public static Mutex GlobalLock;
        private static SynchronizationContext GlobalLockContext;

        internal static void Lock()
        {
            Logger.LogTrace("System lock locking, sync context = {0}", SynchronizationContext.Current);
            GlobalLock = new Mutex(false, GlobalMutexName, out bool createdNew);
            GlobalLockContext = SynchronizationContext.Current;
            try
            {
                GlobalLock.WaitOne();
            }
            catch (AbandonedMutexException e)
            {
                Logger.LogWarning("System lock was abandoned! {0}", e.Message);
            }
            Logger.LogTrace("System lock locked");
        }

        public static bool Lock(int timeout)
        {
            GlobalLock = new Mutex(false, GlobalMutexName, out bool createdNew);
            GlobalLockContext = SynchronizationContext.Current;
            Logger.LogTrace("System lock locking with timeout, sync context = {0}", SynchronizationContext.Current);
            bool success = false;
            try
            {
                success = GlobalLock.WaitOne(timeout);
            }
            catch(AbandonedMutexException e)
            {
                Logger.LogWarning("System lock was abandoned! {0}", e.Message);
                success = true;
            }
            Logger.LogTrace("System lock locked = {}", success);
            return success;
        }

        public static void Unlock()
        {
            Logger.LogTrace("System lock releasing, sync context = {0}", SynchronizationContext.Current);
            try
            {
                if(GlobalLockContext != null)
                {
                    GlobalLockContext.Post((a) =>
                    {
                        GlobalLock.ReleaseMutex();
                    }, null);
                }
                else
                {
                    GlobalLock.ReleaseMutex();
                }
            }
            catch(Exception e)
            {
                Logger.LogWarning("System lock failed to unlock! {0}\n{1}", e.Message, e.StackTrace);
            }
            Logger.LogTrace("System lock released");
        }

        public static EventWaitHandle OpenResetEventSet()
        {
            Logger.LogTrace("OpenResetEventSet()");
            var handle = new EventWaitHandle(true, EventResetMode.ManualReset, GlobalEventWaitHandleName, out bool createdNew);
            if(!createdNew)
            {
                Logger.LogTrace("OpenResetEventSet() setting old event");
                handle.Set();
            }
            return handle;
        }

        public static EventWaitHandle OpenResetEventUnset()
        {
            Logger.LogTrace("OpenResetEventUnset()");
            return new EventWaitHandle(false, EventResetMode.ManualReset, GlobalEventWaitHandleName, out bool createdNew);
        }

        public static FileStream CreateTmpFile(string name)
        {
            return File.Open(ApplicationData.Current.LocalCacheFolder.Path + Path.AltDirectorySeparatorChar + name, FileMode.Create, FileAccess.ReadWrite);
        }

        public static string GetAppStartMessage()
        {
            var version = Package.Current.Id.Version;
            return
                "-------------------------------------------------\n" +
                String.Format("    Signal-Windows {0}.{1}.{2}.{3} starting\n", version.Major, version.Minor, version.Build, version.Revision) +
                "-------------------------------------------------\n";
        }

        public static string GetBGStartMessage()
        {
            var version = Package.Current.Id.Version;
            return
                "-------------------------------------------------\n" +
                String.Format("    Signal-Windows BG {0}.{1}.{2}.{3} starting\n", version.Major, version.Minor, version.Build, version.Revision) +
                "-------------------------------------------------\n";
        }
    }

    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength) // thanks to https://stackoverflow.com/a/2776689/1569755
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
