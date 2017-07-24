using libsignal;
using libsignal.state;
using libsignalservice.util;
using System.Collections.Generic;

namespace Signal_Windows.Storage
{
    public class Store : SignalProtocolStore
    {
        public IdentityKeyPair GetIdentityKeyPair()
        {
            return SignalDBContext.GetIdentityKeyPair();
        }

        public uint GetLocalRegistrationId()
        {
            return SignalDBContext.GetLocalRegistrationId();
        }

        public bool SaveIdentity(SignalProtocolAddress address, IdentityKey identityKey)
        {
            SignalDBContext.SaveIdentityLocked(address.Name, Base64.encodeBytes(identityKey.serialize()));
            return true;
        }

        public bool IsTrustedIdentity(SignalProtocolAddress address, IdentityKey identityKey)
        {
            string savedIdentity = SignalDBContext.GetIdentityLocked(address.Name);
            if (savedIdentity == null)
            {
                return true;
            }
            else
            {
                return savedIdentity == Base64.encodeBytes(identityKey.serialize());
            }
        }

        public PreKeyRecord LoadPreKey(uint preKeyId)
        {
            return SignalDBContext.LoadPreKey(preKeyId);
        }

        public void StorePreKey(uint preKeyId, PreKeyRecord record)
        {
            SignalDBContext.StorePreKey(preKeyId, record);
        }

        public bool ContainsPreKey(uint preKeyId)
        {
            return SignalDBContext.ContainsPreKey(preKeyId);
        }

        public void RemovePreKey(uint preKeyId)
        {
            SignalDBContext.RemovePreKey(preKeyId);
        }

        public SessionRecord LoadSession(SignalProtocolAddress address)
        {
            return SignalDBContext.LoadSession(address);
        }

        public List<uint> GetSubDeviceSessions(string name)
        {
            return SignalDBContext.GetSubDeviceSessions(name);
        }

        public void StoreSession(SignalProtocolAddress address, SessionRecord record)
        {
            SignalDBContext.StoreSession(address, record);
        }

        public bool ContainsSession(SignalProtocolAddress address)
        {
            return SignalDBContext.ContainsSession(address);
        }

        public void DeleteSession(SignalProtocolAddress address)
        {
            SignalDBContext.DeleteSession(address);
        }

        public void DeleteAllSessions(string name)
        {
            SignalDBContext.DeleteAllSessions(name);
        }

        public SignedPreKeyRecord LoadSignedPreKey(uint signedPreKeyId)
        {
            return SignalDBContext.LoadSignedPreKey(signedPreKeyId);
        }

        public List<SignedPreKeyRecord> LoadSignedPreKeys()
        {
            return SignalDBContext.LoadSignedPreKeys();
        }

        public void StoreSignedPreKey(uint signedPreKeyId, SignedPreKeyRecord record)
        {
            SignalDBContext.StoreSignedPreKey(signedPreKeyId, record);
        }

        public bool ContainsSignedPreKey(uint signedPreKeyId)
        {
            return SignalDBContext.ContainsSignedPreKey(signedPreKeyId);
        }

        public void RemoveSignedPreKey(uint signedPreKeyId)
        {
            SignalDBContext.RemoveSignedPreKey(signedPreKeyId);
        }
    }
}