namespace Signal_Windows.Models
{
    public class SignalThread
    {
        public ulong Id { get; set; }
        public string ThreadId { get; set; }
        public string ThreadDisplayName { get; set; }
        public long LastActiveTimestamp { get; set; }
        public string LastMessage { get; set; }
        public string AvatarFile { get; set; }
        public uint Unread { get; set; }
    }
}