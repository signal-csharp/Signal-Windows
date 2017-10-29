namespace Signal_Windows.Lib.Models
{
    public class SignalIdentity
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public string IdentityKey { get; set; }
        public VerifiedStatus VerifiedStatus { get; set; }
    }

    public enum VerifiedStatus
    {
        Default = 0,
        Verified = 1,
        Unverified = 2
    }
}