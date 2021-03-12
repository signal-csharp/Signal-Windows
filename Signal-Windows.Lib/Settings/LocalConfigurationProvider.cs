using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Windows.Storage;

namespace Signal_Windows.Lib.Settings
{
    internal class LocalConfigurationProvider : ConfigurationProvider
    {
        public LocalConfigurationProvider(LocalConfigurationSource localConfigurationSource)
        {
            var appSettingsFile = WaitAndGet(StorageFile.GetFileFromPathAsync($@"{localConfigurationSource.JsonSettingsFilePath}").AsTask());
            JObject o = JObject.Parse(WaitAndGet(FileIO.ReadTextAsync(appSettingsFile).AsTask()));
            foreach (JProperty token in o["signalSettings"])
            {
                if (token.Value.Type == JTokenType.String)
                {
                    Data.Add(token.Name, (string)token.Value);
                }
            }
        }

        private T WaitAndGet<T>(Task<T> t)
        {
            t.Wait();
            return t.Result;
        }
    }
}
