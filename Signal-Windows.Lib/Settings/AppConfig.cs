using Microsoft.Extensions.Configuration;
using Windows.ApplicationModel;

namespace Signal_Windows.Lib.Settings
{
    public class AppConfig
    {
        private readonly IConfigurationRoot configurationRoot;

        public AppConfig()
        {
            string jsonSettingsFilePath =
                $@"{Package.Current.InstalledLocation.Path}\Signal-Windows.Lib\Settings\";

            bool useStaging = false;
            if (useStaging)
            {
                jsonSettingsFilePath += "appsettings.json";
            }
            else
            {
                jsonSettingsFilePath += "appsettings.production.json";
            }

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .Add(new LocalConfigurationSource(jsonSettingsFilePath));

            configurationRoot = builder.Build();
        }

        public SignalSettings GetSignalSettings()
        {
            return new SignalSettings(GetSection<string>(nameof(SignalSettings.ServiceUrl)),
                GetSection<string>(nameof(SignalSettings.ContactDiscoveryServiceUrl)),
                GetSection<string>(nameof(SignalSettings.ContactDiscoveryServiceEnclaveId)));
        }

        private T GetSection<T>(string key)
        {
            return configurationRoot.GetSection(key).Get<T>();
        }
    }
}
