using System.Collections.Generic;

namespace Signal_Windows.Lib.Settings
{
    public sealed class SignalSettings
    {
        public string ServiceUrl { get; private set; }
        public List<string> Cdn1Urls { get; private set; }
        public List<string> Cdn2Urls { get; private set; }
        public string ContactDiscoveryServiceUrl { get; private set; }
        public string ContactDiscoveryServiceEnclaveId { get; private set; }

        public SignalSettings(string serviceUrl,
            List<string> cdn1Urls,
            List<string> cdn2Urls,
            string contactDiscoveryServiceUrl,
            string contactDiscoveryServiceEnclaveId)
        {
            ServiceUrl = serviceUrl;
            Cdn1Urls = cdn1Urls;
            Cdn2Urls = cdn2Urls;
            ContactDiscoveryServiceUrl = contactDiscoveryServiceUrl;
            ContactDiscoveryServiceEnclaveId = contactDiscoveryServiceEnclaveId;
        }
    }
}
