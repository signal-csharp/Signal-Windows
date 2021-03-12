namespace Signal_Windows.Models
{
    // Database model
    public class SignalSession
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public uint DeviceId { get; set; }
        public string Session { get; set; }
    }
}