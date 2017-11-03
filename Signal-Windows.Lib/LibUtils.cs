using libsignalservice.push;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Signal_Windows.Lib
{
    class LibUtils
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
