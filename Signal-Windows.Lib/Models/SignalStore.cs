using System;

namespace Signal_Windows.Models
{
    // Database model
    public class SignalStore
    {
        public long Id { get; set; }
        public uint DeviceId { get; set; }

        /// <summary>
        /// Our phone number.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Our Signal UUID.
        /// </summary>
        public Guid? OwnGuid { get; set; }
        public string Password { get; set; }
        public string SignalingKey { get; set; }
        public uint PreKeyIdOffset { get; set; }
        public uint NextSignedPreKeyId { get; set; }
        public bool Registered { get; set; }
        public string IdentityKeyPair { get; set; }
        public uint RegistrationId { get; set; }
    }
}