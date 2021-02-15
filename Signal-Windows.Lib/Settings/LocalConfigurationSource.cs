using Microsoft.Extensions.Configuration;

namespace Signal_Windows.Lib.Settings
{
    internal class LocalConfigurationSource : IConfigurationSource
    {
        public string JsonSettingsFilePath { get; }

        public LocalConfigurationSource(string jsonSettingsFilePath)
        {
            JsonSettingsFilePath = jsonSettingsFilePath;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new LocalConfigurationProvider(this);
        }
    }
}
