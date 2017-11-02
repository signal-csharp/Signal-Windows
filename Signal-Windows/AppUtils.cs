using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace Signal_Windows
{
    public class AppUtils
    {
        public static void TryVibrate(bool quick)
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice"))
            {
                Windows.Phone.Devices.Notification.VibrationDevice.GetDefault().Vibrate(TimeSpan.FromMilliseconds(quick ? 100 : 500));
            }
        }

        public static bool IsWindowsMobile()
        {
            return ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1);
        }
    }
}
