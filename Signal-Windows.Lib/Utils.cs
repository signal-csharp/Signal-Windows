using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Signal_Windows
{
    public static class Utils
    {
        public const string RED = "red";
        public const string PINK = "pink";
        public const string PURPLE = "purple";
        public const string DEEP_PURPLE = "deep_purple";
        public const string INDIGO = "indigo";
        public const string BLUE = "blue";
        public const string LIGHT_BLUE = "light_blue";
        public const string CYAN = "cyan";
        public const string TEAL = "teal";
        public const string GREEN = "green";
        public const string LIGHT_GREEN = "light_green";
        public const string ORANGE = "orange";
        public const string DEEP_ORANGE = "deep_orange";
        public const string AMBER = "amber";
        public const string BLUE_GREY = "blue_grey";
        public const string GREY = "grey";

        public static string[] Colors = {
            RED,
            PINK,
            PURPLE,
            DEEP_PURPLE,
            INDIGO,
            BLUE,
            LIGHT_BLUE,
            CYAN,
            TEAL,
            GREEN,
            LIGHT_GREEN,
            ORANGE,
            DEEP_ORANGE,
            AMBER,
            BLUE_GREY};

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
        public static SolidColorBrush BackgroundOutgoing = GetSolidColorBrush(255, "#f3f3f3");
        public static SolidColorBrush ForegroundIncoming = GetSolidColorBrush(255, "#ffffff");
        public static SolidColorBrush ForegroundOutgoing = GetSolidColorBrush(255, "#454545");

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
                case RED: return Red;
                case PINK: return Pink;
                case PURPLE: return Purple;
                case DEEP_PURPLE: return Deep_Purple;
                case INDIGO: return Indigo;
                case BLUE: return Blue;
                case LIGHT_BLUE: return Light_Blue;
                case CYAN: return Cyan;
                case TEAL: return Teal;
                case GREEN: return Green;
                case LIGHT_GREEN: return Light_Green;
                case ORANGE: return Orange;
                case DEEP_ORANGE: return Deep_Orange;
                case AMBER: return Amber;
                case BLUE_GREY: return Blue_Grey;
                case GREY: return Grey;
                case "system": return new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
                default: return Default;
            }
        }

        public static string GetColorFromBrush(SolidColorBrush brush)
        {
            Color color = brush.Color;
            if (color == Red.Color) { return RED; }
            else if (color == Pink.Color) { return PINK; }
            else if (color == Purple.Color) { return PURPLE; }
            else if (color == Deep_Purple.Color) { return DEEP_PURPLE; }
            else if (color == Indigo.Color) { return INDIGO; }
            else if (color == Blue.Color) { return BLUE; }
            else if (color == Light_Blue.Color) { return LIGHT_BLUE; }
            else if (color == Cyan.Color) { return CYAN; }
            else if (color == Teal.Color) { return TEAL; }
            else if (color == Green.Color) { return GREEN; }
            else if (color == Light_Green.Color) { return LIGHT_GREEN; }
            else if (color == Orange.Color) { return ORANGE; }
            else if (color == Deep_Orange.Color) { return DEEP_ORANGE; }
            else if (color == Amber.Color) { return AMBER; }
            else if (color == Blue_Grey.Color) { return BLUE_GREY; }
            else if (color == Grey.Color) { return GREY; }
            else { return GREY; }
        }

        public static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> collection)
        {
            foreach (var item in collection)
            {
                observableCollection.Add(item);
            }
        }

        public static string CalculateDefaultColor(string title)
        {
            return Colors[Math.Abs(JavaStringHashCode(title)) % Colors.Length];
        }

        public static SolidColorBrush GetDefaultColor(string title)
        {
            return GetBrushFromColor(CalculateDefaultColor(title));
        }

        public static int JavaStringHashCode(string str)
        {
            int h = 0;
            if (str.Length > 0)
            {
                for (int i = 0; i < str.Length; i++)
                {
                    h = 31 * h + str[i];
                }
            }
            return h;
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

        public static string GetInitials(string name)
        {
            return name.Length == 0 ? "#" : name.Substring(0, 1);
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

        public static string GetTimestamp(long timestamp)
        {
            if (timestamp == 0) return String.Empty;

            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp / 1000);
            DateTime dt = dateTimeOffset.UtcDateTime.ToLocalTime();
            return GetTimestamp(dt);
        }

        public static string GetTimestamp(DateTime dateTime)
        {
            string formattedTimestamp = string.Empty;
            DateTime now = DateTimeOffset.Now.LocalDateTime;
            if (GetMidnightDateTime(now) - GetMidnightDateTime(dateTime) < TimeSpan.FromDays(7))
            {
                if (now.Day == dateTime.Day)
                {
                    // on the same day
                    formattedTimestamp = dateTime.ToString("t");
                }
                else
                {
                    // within the last week
                    formattedTimestamp = $"{dateTime.ToString("ddd")}, {dateTime.ToString("t")}";
                }
            }
            else
            {
                if (now.Year == dateTime.Year)
                {
                    // greater than one week and in the same year
                    formattedTimestamp = $"{dateTime.ToString("M")}, {dateTime.ToString("t")}";
                }
                else
                {
                    // not in the same year
                    formattedTimestamp = dateTime.ToString("g");
                }
            }
            return formattedTimestamp;
        }

        public static DateTime GetMidnightDateTime(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, dateTime.Kind);
        }

        public static string GetCountryISO()
        {
            var c = CultureInfo.CurrentCulture.Name;
            return c.Substring(c.Length - 2);
        }

        public static bool ContainsCaseInsensitive(this string str, string value)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(str, value, CompareOptions.IgnoreCase) >= 0;
        }

        public static string GetCountryCode(string ISO3166) //https://stackoverflow.com/questions/34837436/uwp-get-country-phone-number-prefix
        {
            var dictionary = new Dictionary<string, string>();

            dictionary.Add("AC", "+247");
            dictionary.Add("AD", "+376");
            dictionary.Add("AE", "+971");
            dictionary.Add("AF", "+93");
            dictionary.Add("AG", "+1-268");
            dictionary.Add("AI", "+1-264");
            dictionary.Add("AL", "+355");
            dictionary.Add("AM", "+374");
            dictionary.Add("AN", "+599");
            dictionary.Add("AO", "+244");
            dictionary.Add("AR", "+54");
            dictionary.Add("AS", "+1-684");
            dictionary.Add("AT", "+43");
            dictionary.Add("AU", "+61");
            dictionary.Add("AW", "+297");
            dictionary.Add("AX", "+358-18");
            dictionary.Add("AZ", "+994"); // or +374-97
            dictionary.Add("BA", "+387");
            dictionary.Add("BB", "+1-246");
            dictionary.Add("BD", "+880");
            dictionary.Add("BE", "+32");
            dictionary.Add("BF", "+226");
            dictionary.Add("BG", "+359");
            dictionary.Add("BH", "+973");
            dictionary.Add("BI", "+257");
            dictionary.Add("BJ", "+229");
            dictionary.Add("BM", "+1-441");
            dictionary.Add("BN", "+673");
            dictionary.Add("BO", "+591");
            dictionary.Add("BR", "+55");
            dictionary.Add("BS", "+1-242");
            dictionary.Add("BT", "+975");
            dictionary.Add("BW", "+267");
            dictionary.Add("BY", "+375");
            dictionary.Add("BZ", "+501");
            dictionary.Add("CA", "+1");
            dictionary.Add("CC", "+61");
            dictionary.Add("CD", "+243");
            dictionary.Add("CF", "+236");
            dictionary.Add("CG", "+242");
            dictionary.Add("CH", "+41");
            dictionary.Add("CI", "+225");
            dictionary.Add("CK", "+682");
            dictionary.Add("CL", "+56");
            dictionary.Add("CM", "+237");
            dictionary.Add("CN", "+86");
            dictionary.Add("CO", "+57");
            dictionary.Add("CR", "+506");
            dictionary.Add("CS", "+381");
            dictionary.Add("CU", "+53");
            dictionary.Add("CV", "+238");
            dictionary.Add("CX", "+61");
            dictionary.Add("CY", "+357"); // or +90-392
            dictionary.Add("CZ", "+420");
            dictionary.Add("DE", "+49");
            dictionary.Add("DJ", "+253");
            dictionary.Add("DK", "+45");
            dictionary.Add("DM", "+1-767");
            dictionary.Add("DO", "+1-809"); // and 1-829?
            dictionary.Add("DZ", "+213");
            dictionary.Add("EC", "+593");
            dictionary.Add("EE", "+372");
            dictionary.Add("EG", "+20");
            dictionary.Add("EH", "+212");
            dictionary.Add("ER", "+291");
            dictionary.Add("ES", "+34");
            dictionary.Add("ET", "+251");
            dictionary.Add("FI", "+358");
            dictionary.Add("FJ", "+679");
            dictionary.Add("FK", "+500");
            dictionary.Add("FM", "+691");
            dictionary.Add("FO", "+298");
            dictionary.Add("FR", "+33");
            dictionary.Add("GA", "+241");
            dictionary.Add("GB", "+44");
            dictionary.Add("GD", "+1-473");
            dictionary.Add("GE", "+995");
            dictionary.Add("GF", "+594");
            dictionary.Add("GG", "+44");
            dictionary.Add("GH", "+233");
            dictionary.Add("GI", "+350");
            dictionary.Add("GL", "+299");
            dictionary.Add("GM", "+220");
            dictionary.Add("GN", "+224");
            dictionary.Add("GP", "+590");
            dictionary.Add("GQ", "+240");
            dictionary.Add("GR", "+30");
            dictionary.Add("GT", "+502");
            dictionary.Add("GU", "+1-671");
            dictionary.Add("GW", "+245");
            dictionary.Add("GY", "+592");
            dictionary.Add("HK", "+852");
            dictionary.Add("HN", "+504");
            dictionary.Add("HR", "+385");
            dictionary.Add("HT", "+509");
            dictionary.Add("HU", "+36");
            dictionary.Add("ID", "+62");
            dictionary.Add("IE", "+353");
            dictionary.Add("IL", "+972");
            dictionary.Add("IM", "+44");
            dictionary.Add("IN", "+91");
            dictionary.Add("IO", "+246");
            dictionary.Add("IQ", "+964");
            dictionary.Add("IR", "+98");
            dictionary.Add("IS", "+354");
            dictionary.Add("IT", "+39");
            dictionary.Add("JE", "+44");
            dictionary.Add("JM", "+1-876");
            dictionary.Add("JO", "+962");
            dictionary.Add("JP", "+81");
            dictionary.Add("KE", "+254");
            dictionary.Add("KG", "+996");
            dictionary.Add("KH", "+855");
            dictionary.Add("KI", "+686");
            dictionary.Add("KM", "+269");
            dictionary.Add("KN", "+1-869");
            dictionary.Add("KP", "+850");
            dictionary.Add("KR", "+82");
            dictionary.Add("KW", "+965");
            dictionary.Add("KY", "+1-345");
            dictionary.Add("KZ", "+7");
            dictionary.Add("LA", "+856");
            dictionary.Add("LB", "+961");
            dictionary.Add("LC", "+1-758");
            dictionary.Add("LI", "+423");
            dictionary.Add("LK", "+94");
            dictionary.Add("LR", "+231");
            dictionary.Add("LS", "+266");
            dictionary.Add("LT", "+370");
            dictionary.Add("LU", "+352");
            dictionary.Add("LV", "+371");
            dictionary.Add("LY", "+218");
            dictionary.Add("MA", "+212");
            dictionary.Add("MC", "+377");
            dictionary.Add("MD", "+373"); // or +373-533
            dictionary.Add("ME", "+382");
            dictionary.Add("MG", "+261");
            dictionary.Add("MH", "+692");
            dictionary.Add("MK", "+389");
            dictionary.Add("ML", "+223");
            dictionary.Add("MM", "+95");
            dictionary.Add("MN", "+976");
            dictionary.Add("MO", "+853");
            dictionary.Add("MP", "+1-670");
            dictionary.Add("MQ", "+596");
            dictionary.Add("MR", "+222");
            dictionary.Add("MS", "+1-664");
            dictionary.Add("MT", "+356");
            dictionary.Add("MU", "+230");
            dictionary.Add("MV", "+960");
            dictionary.Add("MW", "+265");
            dictionary.Add("MX", "+52");
            dictionary.Add("MY", "+60");
            dictionary.Add("MZ", "+258");
            dictionary.Add("NA", "+264");
            dictionary.Add("NC", "+687");
            dictionary.Add("NE", "+227");
            dictionary.Add("NF", "+672");
            dictionary.Add("NG", "+234");
            dictionary.Add("NI", "+505");
            dictionary.Add("NL", "+31");
            dictionary.Add("NO", "+47");
            dictionary.Add("NP", "+977");
            dictionary.Add("NR", "+674");
            dictionary.Add("NU", "+683");
            dictionary.Add("NZ", "+64");
            dictionary.Add("OM", "+968");
            dictionary.Add("PA", "+507");
            dictionary.Add("PE", "+51");
            dictionary.Add("PF", "+689");
            dictionary.Add("PG", "+675");
            dictionary.Add("PH", "+63");
            dictionary.Add("PK", "+92");
            dictionary.Add("PL", "+48");
            dictionary.Add("PM", "+508");
            dictionary.Add("PR", "+1-787"); // and 1-939 ?
            dictionary.Add("PS", "+970");
            dictionary.Add("PT", "+351");
            dictionary.Add("PW", "+680");
            dictionary.Add("PY", "+595");
            dictionary.Add("QA", "+974");
            dictionary.Add("RE", "+262");
            dictionary.Add("RO", "+40");
            dictionary.Add("RS", "+381");
            dictionary.Add("RU", "+7");
            dictionary.Add("RW", "+250");
            dictionary.Add("SA", "+966");
            dictionary.Add("SB", "+677");
            dictionary.Add("SC", "+248");
            dictionary.Add("SD", "+249");
            dictionary.Add("SE", "+46");
            dictionary.Add("SG", "+65");
            dictionary.Add("SH", "+290");
            dictionary.Add("SI", "+386");
            dictionary.Add("SJ", "+47");
            dictionary.Add("SK", "+421");
            dictionary.Add("SL", "+232");
            dictionary.Add("SM", "+378");
            dictionary.Add("SN", "+221");
            dictionary.Add("SO", "+252");
            dictionary.Add("SR", "+597");
            dictionary.Add("ST", "+239");
            dictionary.Add("SV", "+503");
            dictionary.Add("SY", "+963");
            dictionary.Add("SZ", "+268");
            dictionary.Add("TA", "+290");
            dictionary.Add("TC", "+1-649");
            dictionary.Add("TD", "+235");
            dictionary.Add("TG", "+228");
            dictionary.Add("TH", "+66");
            dictionary.Add("TJ", "+992");
            dictionary.Add("TK", "+690");
            dictionary.Add("TL", "+670");
            dictionary.Add("TM", "+993");
            dictionary.Add("TN", "+216");
            dictionary.Add("TO", "+676");
            dictionary.Add("TR", "+90");
            dictionary.Add("TT", "+1-868");
            dictionary.Add("TV", "+688");
            dictionary.Add("TW", "+886");
            dictionary.Add("TZ", "+255");
            dictionary.Add("UA", "+380");
            dictionary.Add("UG", "+256");
            dictionary.Add("US", "+1");
            dictionary.Add("UY", "+598");
            dictionary.Add("UZ", "+998");
            dictionary.Add("VA", "+379");
            dictionary.Add("VC", "+1-784");
            dictionary.Add("VE", "+58");
            dictionary.Add("VG", "+1-284");
            dictionary.Add("VI", "+1-340");
            dictionary.Add("VN", "+84");
            dictionary.Add("VU", "+678");
            dictionary.Add("WF", "+681");
            dictionary.Add("WS", "+685");
            dictionary.Add("YE", "+967");
            dictionary.Add("YT", "+262");
            dictionary.Add("ZA", "+27");
            dictionary.Add("ZM", "+260");
            dictionary.Add("ZW", "+263");

            if (dictionary.ContainsKey(ISO3166))
            {
                return dictionary[ISO3166];
            }
            else
            {
                return null;
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

        public void AddSilently(T t)
        {
            bool old = _suppressNotification;
            _suppressNotification = true;
            Add(t);
            _suppressNotification = old;
        }

        public void ForceCollectionChanged()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public static class CountryArrays
    {
        /// <summary>
        /// Country names
        /// </summary>
        public static string[] Names = new string[]
        {
            "Afghanistan",
            "Albania",
            "Algeria",
            "American Samoa",
            "Andorra",
            "Angola",
            "Anguilla",
            "Antarctica",
            "Antigua and Barbuda",
            "Argentina",
            "Armenia",
            "Aruba",
            "Australia",
            "Austria",
            "Azerbaijan",
            "Bahamas",
            "Bahrain",
            "Bangladesh",
            "Barbados",
            "Belarus",
            "Belgium",
            "Belize",
            "Benin",
            "Bermuda",
            "Bhutan",
            "Bolivia",
            "Bosnia and Herzegovina",
            "Botswana",
            "Bouvet Island",
            "Brazil",
            "British Indian Ocean Territory",
            "Brunei Darussalam",
            "Bulgaria",
            "Burkina Faso",
            "Burundi",
            "Cambodia",
            "Cameroon",
            "Canada",
            "Cape Verde",
            "Cayman Islands",
            "Central African Republic",
            "Chad",
            "Chile",
            "China",
            "Christmas Island",
            "Cocos (Keeling) Islands",
            "Colombia",
            "Comoros",
            "Congo",
            "Congo, the Democratic Republic of the",
            "Cook Islands",
            "Costa Rica",
            "Cote D'Ivoire",
            "Croatia",
            "Cuba",
            "Cyprus",
            "Czech Republic",
            "Denmark",
            "Djibouti",
            "Dominica",
            "Dominican Republic",
            "Ecuador",
            "Egypt",
            "El Salvador",
            "Equatorial Guinea",
            "Eritrea",
            "Estonia",
            "Ethiopia",
            "Falkland Islands (Malvinas)",
            "Faroe Islands",
            "Fiji",
            "Finland",
            "France",
            "French Guiana",
            "French Polynesia",
            "French Southern Territories",
            "Gabon",
            "Gambia",
            "Georgia",
            "Germany",
            "Ghana",
            "Gibraltar",
            "Greece",
            "Greenland",
            "Grenada",
            "Guadeloupe",
            "Guam",
            "Guatemala",
            "Guinea",
            "Guinea-Bissau",
            "Guyana",
            "Haiti",
            "Heard Island and Mcdonald Islands",
            "Holy See (Vatican City State)",
            "Honduras",
            "Hong Kong",
            "Hungary",
            "Iceland",
            "India",
            "Indonesia",
            "Iran, Islamic Republic of",
            "Iraq",
            "Ireland",
            "Israel",
            "Italy",
            "Jamaica",
            "Japan",
            "Jordan",
            "Kazakhstan",
            "Kenya",
            "Kiribati",
            "Korea, Democratic People's Republic of",
            "Korea, Republic of",
            "Kuwait",
            "Kyrgyzstan",
            "Lao People's Democratic Republic",
            "Latvia",
            "Lebanon",
            "Lesotho",
            "Liberia",
            "Libyan Arab Jamahiriya",
            "Liechtenstein",
            "Lithuania",
            "Luxembourg",
            "Macao",
            "Macedonia, the Former Yugoslav Republic of",
            "Madagascar",
            "Malawi",
            "Malaysia",
            "Maldives",
            "Mali",
            "Malta",
            "Marshall Islands",
            "Martinique",
            "Mauritania",
            "Mauritius",
            "Mayotte",
            "Mexico",
            "Micronesia, Federated States of",
            "Moldova, Republic of",
            "Monaco",
            "Mongolia",
            "Montserrat",
            "Morocco",
            "Mozambique",
            "Myanmar",
            "Namibia",
            "Nauru",
            "Nepal",
            "Netherlands",
            "Netherlands Antilles",
            "New Caledonia",
            "New Zealand",
            "Nicaragua",
            "Niger",
            "Nigeria",
            "Niue",
            "Norfolk Island",
            "Northern Mariana Islands",
            "Norway",
            "Oman",
            "Pakistan",
            "Palau",
            "Palestinian Territory, Occupied",
            "Panama",
            "Papua New Guinea",
            "Paraguay",
            "Peru",
            "Philippines",
            "Pitcairn",
            "Poland",
            "Portugal",
            "Puerto Rico",
            "Qatar",
            "Reunion",
            "Romania",
            "Russian Federation",
            "Rwanda",
            "Saint Helena",
            "Saint Kitts and Nevis",
            "Saint Lucia",
            "Saint Pierre and Miquelon",
            "Saint Vincent and the Grenadines",
            "Samoa",
            "San Marino",
            "Sao Tome and Principe",
            "Saudi Arabia",
            "Senegal",
            "Serbia and Montenegro",
            "Seychelles",
            "Sierra Leone",
            "Singapore",
            "Slovakia",
            "Slovenia",
            "Solomon Islands",
            "Somalia",
            "South Africa",
            "South Georgia and the South Sandwich Islands",
            "Spain",
            "Sri Lanka",
            "Sudan",
            "Suriname",
            "Svalbard and Jan Mayen",
            "Swaziland",
            "Sweden",
            "Switzerland",
            "Syrian Arab Republic",
            "Taiwan, Province of China",
            "Tajikistan",
            "Tanzania, United Republic of",
            "Thailand",
            "Timor-Leste",
            "Togo",
            "Tokelau",
            "Tonga",
            "Trinidad and Tobago",
            "Tunisia",
            "Turkey",
            "Turkmenistan",
            "Turks and Caicos Islands",
            "Tuvalu",
            "Uganda",
            "Ukraine",
            "United Arab Emirates",
            "United Kingdom",
            "United States",
            "United States Minor Outlying Islands",
            "Uruguay",
            "Uzbekistan",
            "Vanuatu",
            "Venezuela",
            "Viet Nam",
            "Virgin Islands, British",
            "Virgin Islands, US",
            "Wallis and Futuna",
            "Western Sahara",
            "Yemen",
            "Zambia",
            "Zimbabwe",
        };

        /// <summary>
        /// Country abbreviations
        /// </summary>
        public static string[] Abbreviations = new string[]
        {
            "AF",
            "AL",
            "DZ",
            "AS",
            "AD",
            "AO",
            "AI",
            "AQ",
            "AG",
            "AR",
            "AM",
            "AW",
            "AU",
            "AT",
            "AZ",
            "BS",
            "BH",
            "BD",
            "BB",
            "BY",
            "BE",
            "BZ",
            "BJ",
            "BM",
            "BT",
            "BO",
            "BA",
            "BW",
            "BV",
            "BR",
            "IO",
            "BN",
            "BG",
            "BF",
            "BI",
            "KH",
            "CM",
            "CA",
            "CV",
            "KY",
            "CF",
            "TD",
            "CL",
            "CN",
            "CX",
            "CC",
            "CO",
            "KM",
            "CG",
            "CD",
            "CK",
            "CR",
            "CI",
            "HR",
            "CU",
            "CY",
            "CZ",
            "DK",
            "DJ",
            "DM",
            "DO",
            "EC",
            "EG",
            "SV",
            "GQ",
            "ER",
            "EE",
            "ET",
            "FK",
            "FO",
            "FJ",
            "FI",
            "FR",
            "GF",
            "PF",
            "TF",
            "GA",
            "GM",
            "GE",
            "DE",
            "GH",
            "GI",
            "GR",
            "GL",
            "GD",
            "GP",
            "GU",
            "GT",
            "GN",
            "GW",
            "GY",
            "HT",
            "HM",
            "VA",
            "HN",
            "HK",
            "HU",
            "IS",
            "IN",
            "ID",
            "IR",
            "IQ",
            "IE",
            "IL",
            "IT",
            "JM",
            "JP",
            "JO",
            "KZ",
            "KE",
            "KI",
            "KP",
            "KR",
            "KW",
            "KG",
            "LA",
            "LV",
            "LB",
            "LS",
            "LR",
            "LY",
            "LI",
            "LT",
            "LU",
            "MO",
            "MK",
            "MG",
            "MW",
            "MY",
            "MV",
            "ML",
            "MT",
            "MH",
            "MQ",
            "MR",
            "MU",
            "YT",
            "MX",
            "FM",
            "MD",
            "MC",
            "MN",
            "MS",
            "MA",
            "MZ",
            "MM",
            "NA",
            "NR",
            "NP",
            "NL",
            "AN",
            "NC",
            "NZ",
            "NI",
            "NE",
            "NG",
            "NU",
            "NF",
            "MP",
            "NO",
            "OM",
            "PK",
            "PW",
            "PS",
            "PA",
            "PG",
            "PY",
            "PE",
            "PH",
            "PN",
            "PL",
            "PT",
            "PR",
            "QA",
            "RE",
            "RO",
            "RU",
            "RW",
            "SH",
            "KN",
            "LC",
            "PM",
            "VC",
            "WS",
            "SM",
            "ST",
            "SA",
            "SN",
            "CS",
            "SC",
            "SL",
            "SG",
            "SK",
            "SI",
            "SB",
            "SO",
            "ZA",
            "GS",
            "ES",
            "LK",
            "SD",
            "SR",
            "SJ",
            "SZ",
            "SE",
            "CH",
            "SY",
            "TW",
            "TJ",
            "TZ",
            "TH",
            "TL",
            "TG",
            "TK",
            "TO",
            "TT",
            "TN",
            "TR",
            "TM",
            "TC",
            "TV",
            "UG",
            "UA",
            "AE",
            "GB",
            "US",
            "UM",
            "UY",
            "UZ",
            "VU",
            "VE",
            "VN",
            "VG",
            "VI",
            "WF",
            "EH",
            "YE",
            "ZM",
            "ZW"
        };
    };
}
