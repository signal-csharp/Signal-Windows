using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace Signal_Windows
{
    public class Utils
    {
        public static SolidColorBrush Red = GetSolidColorBrush(255, "#EF5350");
        public static SolidColorBrush Pink = GetSolidColorBrush(255, "#EC407A");
        public static SolidColorBrush Purple = GetSolidColorBrush(255, "#AB47BC");
        public static SolidColorBrush Deep_Purple = GetSolidColorBrush(255, "#7E57C2");
        public static SolidColorBrush Indigo = GetSolidColorBrush(255, "#5C6BC0");
        public static SolidColorBrush Blue = GetSolidColorBrush(255, "#2196F3");
        public static SolidColorBrush Light_Blue = GetSolidColorBrush(255, "#03A9F4");
        public static SolidColorBrush Cyan = GetSolidColorBrush(255, "#00BCD4");
        public static SolidColorBrush Teal = GetSolidColorBrush(255, "#009688");
        public static SolidColorBrush Green = GetSolidColorBrush(255, "#4CAF50");
        public static SolidColorBrush Light_Green = GetSolidColorBrush(255, "#7CB342");
        public static SolidColorBrush Orange = GetSolidColorBrush(255, "#FF9800");
        public static SolidColorBrush Deep_Orange = GetSolidColorBrush(255, "#FF5722");
        public static SolidColorBrush Amber = GetSolidColorBrush(255, "#FFB300");
        public static SolidColorBrush Blue_Grey = GetSolidColorBrush(255, "#607D8B");
        public static SolidColorBrush Grey = GetSolidColorBrush(255, "#999999");
        public static SolidColorBrush Default = GetSolidColorBrush(255, "#2090ea");
        public static SolidColorBrush Outgoing = GetSolidColorBrush(255, "#f3f3f3");

        public static SolidColorBrush GetSolidColorBrush(byte opacity, string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte r = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(opacity, r, g, b));
            return myBrush;
        }

        public static SolidColorBrush GetBrushFromColor(string signalcolor)
        {
            switch (signalcolor)
            {
                case "red": return Red;
                case "pink": return Pink;
                case "purple": return Purple;
                case "deep_purple": return Deep_Purple;
                case "indigo": return Indigo;
                case "blue": return Blue;
                case "light_blue": return Light_Blue;
                case "cyan": return Cyan;
                case "teal": return Teal;
                case "green": return Green;
                case "light_green": return Light_Green;
                case "orange": return Orange;
                case "deep_orange": return Deep_Orange;
                case "amber": return Amber;
                case "blue_grey": return Blue_Grey;
                case "grey": return Grey;
                default: return Default;
            }
        }

        public static void EnableBackButton()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        }

        public static void EnableBackButton(EventHandler<BackRequestedEventArgs> handler)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += handler;
        }

        public static void DisableBackButton()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        public static void DisableBackButton(EventHandler<BackRequestedEventArgs> handler)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            SystemNavigationManager.GetForCurrentView().BackRequested -= handler;
        }

        public static PageStyle GetViewStyle(Size s)
        {
            if (s.Width <= 640)
            {
                return PageStyle.Narrow;
            }
            else
            {
                return PageStyle.Wide;
            }
        }
    }

    public enum PageStyle
    {
        Narrow,
        Wide
    }

    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        // credits to Pete Ohanlon: https://peteohanlon.wordpress.com/2008/10/22/bulk-loading-in-observablecollection/
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        public void AddRange(IEnumerable<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            _suppressNotification = true;
            foreach (T item in list)
            {
                Add(item);
            }
            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}