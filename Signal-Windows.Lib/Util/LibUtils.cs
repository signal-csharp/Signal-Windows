using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using libsignal.ecc;
using libsignalmetadatadotnet.certificate;
using libsignalservice;
using libsignalservice.configuration;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib.Settings;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Signal_Windows.Lib
{
    public class LibUtils
    {
        private static readonly ILogger Logger = LibsignalLogging.CreateLogger<LibUtils>();
        public const string GlobalMutexName = "SignalWindowsPrivateMessenger_Mutex";
        public const string GlobalEventWaitHandleName = "SignalWindowsPrivateMessenger_EventWaitHandle";
        public static string UNIDENTIFIED_SENDER_TRUST_ROOT = "BXu6QIKVz5MA8gstzfOgRQGqyLqOwNKHL6INkv3IHWMF";
        public static SignalServiceUrl[] ServiceUrls;
        public static SignalCdnUrl[] Cdn1Urls;
        public static SignalCdnUrl[] Cdn2Urls;
        public static SignalContactDiscoveryUrl[] ContactDiscoveryUrls;
        public static SignalServiceConfiguration ServiceConfiguration;
        public static bool MainPageActive = false;
        public static string USER_AGENT = "Signal-Windows";
        public static uint PREKEY_BATCH_SIZE = 100;
        public static bool WindowActive = false;
        public static Mutex GlobalLock;
        public static HttpClient HttpClient;
        public static AppConfig AppConfig;
        public static SignalSettings SignalSettings;

        static LibUtils()
        {
            HttpClient = new HttpClient();
            AppConfig = new AppConfig();
            SignalSettings = AppConfig.GetSignalSettings();
            ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl(SignalSettings.ServiceUrl) };
            Cdn1Urls = new SignalCdnUrl[] { new SignalCdnUrl(SignalSettings.Cdn1Urls[0]) };
            Cdn2Urls = new SignalCdnUrl[] { new SignalCdnUrl(SignalSettings.Cdn2Urls[0]) };
            ContactDiscoveryUrls = new SignalContactDiscoveryUrl[] { new SignalContactDiscoveryUrl(SignalSettings.ContactDiscoveryServiceUrl) };
            ServiceConfiguration = new SignalServiceConfiguration(ServiceUrls, Cdn1Urls, Cdn2Urls, ContactDiscoveryUrls);
        }

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
            catch (AbandonedMutexException e)
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
                if (GlobalLockContext != null)
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
            catch (Exception e)
            {
                Logger.LogWarning("System lock failed to unlock! {0}\n{1}", e.Message, e.StackTrace);
            }
            Logger.LogTrace("System lock released");
        }

        public static EventWaitHandle OpenResetEventSet()
        {
            Logger.LogTrace("OpenResetEventSet()");
            var handle = new EventWaitHandle(true, EventResetMode.ManualReset, GlobalEventWaitHandleName, out bool createdNew);
            if (!createdNew)
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

        public static CertificateValidator GetCertificateValidator()
        {
            ECPublicKey unidentifiedSenderTrustRoot = Curve.decodePoint(Base64.Decode(UNIDENTIFIED_SENDER_TRUST_ROOT), 0);
            return new CertificateValidator(unidentifiedSenderTrustRoot);
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
