namespace Signal_Windows.Lib.Models
{
    public class SignalSession
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public uint DeviceId { get; set; }
        public string Session { get; set; }
    }
}