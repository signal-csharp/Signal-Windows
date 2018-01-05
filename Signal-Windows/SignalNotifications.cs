using Microsoft.Toolkit.Uwp.Notifications;
using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;

namespace Signal_Windows
{
    class SignalNotifications
    {
        public static void SendMessageNotification(SignalMessage message)
        {
            // notification tags can only be 16 chars (64 after creators update)
            // https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Notifications.ToastNotification#Windows_UI_Notifications_ToastNotification_Tag
            string notificationId = message.ThreadId;
            ToastBindingGeneric toastBinding = new ToastBindingGeneric()
            {
                AppLogoOverride = new ToastGenericAppLogo()
                {
                    Source = "ms-appx:///Assets/LargeTile.scale-100.png",
                    HintCrop = ToastGenericAppLogoCrop.Circle
                }
            };

            var notificationText = GetNotificationText(message);
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
                DisplayTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(message.ReceivedTimestamp)
            };

            ToastNotification toastNotification = new ToastNotification(toastContent.GetXml());
            if (message.Author.ExpiresInSeconds > 0)
            {
                toastNotification.ExpirationTime = DateTime.Now.Add(TimeSpan.FromSeconds(message.Author.ExpiresInSeconds));
            }
            toastNotification.Tag = notificationId;
            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
        }

        private static IList<AdaptiveText> GetNotificationText(SignalMessage message)
        {
            List<AdaptiveText> text = new List<AdaptiveText>();
            AdaptiveText title = new AdaptiveText()
            {
                Text = message.Author.ThreadDisplayName,
                HintMaxLines = 1
            };
            AdaptiveText messageText = new AdaptiveText()
            {
                Text = message.Content.Content,
                HintWrap = true
            };
            text.Add(title);
            text.Add(messageText);
            return text;
        }

        public static void TryVibrate(bool quick)
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice"))
            {
                Windows.Phone.Devices.Notification.VibrationDevice.GetDefault().Vibrate(TimeSpan.FromMilliseconds(quick ? 100 : 500));
            }
        }

        public static void SendTileNotification(SignalMessage message)
        {
            TileBindingContentAdaptive tileBindingContent = new TileBindingContentAdaptive()
            {
                /*
                PeekImage = new TilePeekImage()
                {
                    Source = "ms-appx:///Assets/gambino.png"
                }
                */
            };
            var notificationText = GetNotificationText(message);
            foreach (var item in notificationText)
            {
                tileBindingContent.Children.Add(item);
            }

            TileBinding tileBinding = new TileBinding()
            {
                Content = tileBindingContent
            };

            TileContent tileContent = new TileContent()
            {
                Visual = new TileVisual()
                {
                    TileMedium = tileBinding,
                    TileWide = tileBinding,
                    TileLarge = tileBinding
                }
            };

            TileNotification tileNotification = new TileNotification(tileContent.GetXml());
            if (message.Author.ExpiresInSeconds > 0)
            {
                tileNotification.ExpirationTime = DateTime.Now.Add(TimeSpan.FromSeconds(message.Author.ExpiresInSeconds));
            }
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
        }
    }
}
