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
            return LibsignalDBContext.GetIdentityKeyPair();
        }

        public uint GetLocalRegistrationId()
        {
            return LibsignalDBContext.GetLocalRegistrationId();
        }

        public bool SaveIdentity(SignalProtocolAddress address, IdentityKey identityKey)
        {
            LibsignalDBContext.SaveIdentityLocked(address, Base64.encodeBytes(identityKey.serialize()));
            return true;
        }

        public bool IsTrustedIdentity(SignalProtocolAddress address, IdentityKey identityKey, Direction direction)
        {
            if (direction == Direction.RECEIVING)
            {
                return true;
            }
            string savedIdentity = LibsignalDBContext.GetIdentityLocked(address.Name);
            if (savedIdentity == null)
            {
                return true;
            }
            else
            {
                //TODO compare timestamps & firstUse, see Signal-Android impl
                string identity = Base64.encodeBytes(identityKey.serialize());
                return savedIdentity == Base64.encodeBytes(identityKey.serialize());
            }
        }

        public PreKeyRecord LoadPreKey(uint preKeyId)
        {
            return LibsignalDBContext.LoadPreKey(preKeyId);
        }

        public void StorePreKey(uint preKeyId, PreKeyRecord record)
        {
            LibsignalDBContext.StorePreKey(preKeyId, record);
        }

        public bool ContainsPreKey(uint preKeyId)
        {
            return LibsignalDBContext.ContainsPreKey(preKeyId);
        }

        public void RemovePreKey(uint preKeyId)
        {
            LibsignalDBContext.RemovePreKey(preKeyId);
        }

        public SessionRecord LoadSession(SignalProtocolAddress address)
        {
            return LibsignalDBContext.LoadSession(address);
        }

        public List<uint> GetSubDeviceSessions(string name)
        {
            return LibsignalDBContext.GetSubDeviceSessions(name);
        }

        public void StoreSession(SignalProtocolAddress address, SessionRecord record)
        {
            LibsignalDBContext.StoreSession(address, record);
        }

        public bool ContainsSession(SignalProtocolAddress address)
        {
            return LibsignalDBContext.ContainsSession(address);
        }

        public void DeleteSession(SignalProtocolAddress address)
        {
            LibsignalDBContext.DeleteSession(address);
        }

        public void DeleteAllSessions(string name)
        {
            LibsignalDBContext.DeleteAllSessions(name);
        }

        public SignedPreKeyRecord LoadSignedPreKey(uint signedPreKeyId)
        {
            return LibsignalDBContext.LoadSignedPreKey(signedPreKeyId);
        }

        public List<SignedPreKeyRecord> LoadSignedPreKeys()
        {
            return LibsignalDBContext.LoadSignedPreKeys();
        }

        public void StoreSignedPreKey(uint signedPreKeyId, SignedPreKeyRecord record)
        {
            LibsignalDBContext.StoreSignedPreKey(signedPreKeyId, record);
        }

        public bool ContainsSignedPreKey(uint signedPreKeyId)
        {
            return LibsignalDBContext.ContainsSignedPreKey(signedPreKeyId);
        }

        public void RemoveSignedPreKey(uint signedPreKeyId)
        {
            LibsignalDBContext.RemoveSignedPreKey(signedPreKeyId);
        }
    }
}