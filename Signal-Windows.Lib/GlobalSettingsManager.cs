using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Signal_Windows.Lib
{
    public static class GlobalSettingsManager
    {
        private const string NotificationsContainer = "notifications";
        private const string PrivacyContainer = "privacy";
        private const string AppearanceContainer = "appearance";
        private const string ChatsAndMediaContainer = "chatsandmedia";
        private const string AdvancedContainer = "advanced";

        private const string ShowNotificationText = "ShowNotificationText";
        public enum ShowNotificationTextSettings
        {
            NameAndMessage,
            NameOnly,
            NoNameOrMessage
        }
        private const string BlockScreenshots = "BlockScreenshots";
        private const string EnableReadReceipts = "EnableReadReceipts";
        private const string SpellCheck = "SpellCheck";

        private static ApplicationDataContainer localSettings;
        private static IReadOnlyDictionary<string, ApplicationDataContainer> Containers
        {
            get { return localSettings.Containers; }
        }

        static GlobalSettingsManager()
        {
            localSettings = ApplicationData.Current.LocalSettings;
            var containers = localSettings.Containers;
            if (!containers.ContainsKey(NotificationsContainer))
            {
                localSettings.CreateContainer(NotificationsContainer, ApplicationDataCreateDisposition.Always);
            }
            if (!containers.ContainsKey(PrivacyContainer))
            {
                localSettings.CreateContainer(PrivacyContainer, ApplicationDataCreateDisposition.Always);
            }
            if (!containers.ContainsKey(AppearanceContainer))
            {
                localSettings.CreateContainer(AppearanceContainer, ApplicationDataCreateDisposition.Always);
            }
            if (!containers.ContainsKey(ChatsAndMediaContainer))
            {
                localSettings.CreateContainer(ChatsAndMediaContainer, ApplicationDataCreateDisposition.Always);
            }
            if (!containers.ContainsKey(AdvancedContainer))
            {
                localSettings.CreateContainer(AdvancedContainer, ApplicationDataCreateDisposition.Always);
            }
        }

        public static ShowNotificationTextSettings ShowNotificationTextSetting
        {
            get
            {
                return (ShowNotificationTextSettings)GetSetting(Containers[NotificationsContainer],
                    ShowNotificationText, (int)ShowNotificationTextSettings.NameAndMessage);
            }
            set
            {
                Containers[NotificationsContainer].Values[ShowNotificationText] = (int)value;
            }
        }

        public static bool BlockScreenshotsSetting
        {
            get
            {
                return GetSetting(Containers[PrivacyContainer], BlockScreenshots, false);
            }
            set
            {
                Containers[PrivacyContainer].Values[BlockScreenshots] = value;
            }
        }

        public static bool EnableReadReceiptsSetting
        {
            get
            {
                return GetSetting(Containers[PrivacyContainer], EnableReadReceipts, true);
            }
            set
            {
                Containers[PrivacyContainer].Values[EnableReadReceipts] = value;
            }
        }

        public static bool SpellCheckSetting
        {
            get
            {
                return GetSetting(Containers[ChatsAndMediaContainer], SpellCheck, true);
            }
            set
            {
                Containers[ChatsAndMediaContainer].Values[SpellCheck] = value;
            }
        }

        private static T GetSetting<T>(ApplicationDataContainer container, string key, T defaultValue)
        {
            if (container.Values.ContainsKey(key))
            {
                return (T)container.Values[key];
            }
            else
            {
                container.Values[key] = defaultValue;
                return defaultValue;
            }
        }
    }
}
