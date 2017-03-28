namespace Signal_Windows.Models
{
    public class SignalContact
    {
        public SignalContact()
        {
            ContactDisplayName = "Anonymous";
            Color = "";
            UserName = "";
        }

        public uint Id { get; set; }
        public string UserName { get; set; }
        public string ContactDisplayName { get; set; }
        public string Color { get; set; }
        public long LastActiveTimestamp { get; set; }
        //public bool HasUnread { get; set; }
    }
}