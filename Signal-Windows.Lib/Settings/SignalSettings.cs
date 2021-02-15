namespace Signal_Windows.Lib.Settings
{
    public sealed class SignalSettings
    {
        public string ServiceUrl { get; private set; }
        public string ContactDiscoveryServiceUrl { get; private set; }
        public string ContactDiscoveryServiceEnclaveId { get; private set; }

        public SignalSettings(string serviceUrl, string contactDiscoveryServiceUrl, string contactDiscoveryServiceEnclaveId)
        {
            ServiceUrl = serviceUrl;
            ContactDiscoveryServiceUrl = contactDiscoveryServiceUrl;
            ContactDiscoveryServiceEnclaveId = contactDiscoveryServiceEnclaveId;
        }
    }
}
