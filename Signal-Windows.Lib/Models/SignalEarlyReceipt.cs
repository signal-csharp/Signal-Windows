namespace Signal_Windows.Models
{
    public class SignalEarlyReceipt
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public uint DeviceId { get; set; }
        public long Timestamp { get; set; }
    }
}