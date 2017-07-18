using libsignal;
using libsignal.ecc;
using libsignal.state;
using libsignalservice.push;
using libsignalservice.util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace Signal_Windows.Storage
{
    public class Store : SignalProtocolStore
    {
        [JsonIgnore] public static Store Instance;
        [JsonIgnore] public static string localFolder = ApplicationData.Current.LocalFolder.Path;
        [JsonIgnore] public static ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        [JsonIgnore] public static object Lock = new object();

        [JsonIgnore]
        public static JsonConverter[] Converters = new JsonConverter[] {
                                                new IdentityKeyPairConverter(),
                                                new IdentityKeyConverter(),
                                                new ByteArrayConverter()};

        public int DeviceId { get; set; } = (int)SignalServiceAddress.DEFAULT_DEVICE_ID;
        public String Username { get; set; }
        public String Password { get; set; }
        public String SignalingKey { get; set; }
        public uint PreKeyIdOffset { get; set; }
        public uint NextSignedPreKeyId { get; set; }
        public bool Registered { get; set; } = false;
        public JsonPreKeyStore PreKeyStore { get; set; }
        public JsonIdentityKeyStore IdentityKeyStore { get; set; }
        public JsonSessionStore SessionStore { get; set; }
        public JsonSignedPreKeyStore SignedPreKeyStore { get; set; }

        public Store()
        {
            Instance = this;
        }

        public Store(IdentityKeyPair identityKey, uint registrationId)
        {
            Instance = this;
            PreKeyStore = new JsonPreKeyStore();
            SessionStore = new JsonSessionStore();
            SignedPreKeyStore = new JsonSignedPreKeyStore();
            IdentityKeyStore = new JsonIdentityKeyStore(identityKey, registrationId);
        }

        public void Save()
        {
            try
            {
                using (FileStream fs = File.Open(localFolder + @"\" + LocalSettings.Values["Username"] + "Store.json", FileMode.OpenOrCreate))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    fs.SetLength(0);
                    string s = JsonConvert.SerializeObject(this, Formatting.Indented, Converters);
                    sw.Write(s);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("SignalProtocolStore failed to save!");
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        public IdentityKeyPair GetIdentityKeyPair()
        {
            return IdentityKeyStore.GetIdentityKeyPair();
        }

        public uint GetLocalRegistrationId()
        {
            return IdentityKeyStore.GetLocalRegistrationId();
        }

        public bool SaveIdentity(SignalProtocolAddress address, IdentityKey identityKey)
        {
            lock (Lock)
            {
                SignalDBContext.SaveIdentityLocked(address.Name, Base64.encodeBytes(identityKey.serialize()));
                return true;
            }
        }

        public bool IsTrustedIdentity(SignalProtocolAddress address, IdentityKey identityKey)
        {
            lock (Lock)
            {
                return IdentityKeyStore.IsTrustedIdentity(address, identityKey);
            }
        }

        public PreKeyRecord LoadPreKey(uint preKeyId)
        {
            return PreKeyStore.LoadPreKey(preKeyId);
        }

        public void StorePreKey(uint preKeyId, PreKeyRecord record)
        {
            Debug.WriteLine(String.Format("storing prekey {0} {1}", preKeyId, record));
            PreKeyStore.StorePreKey(preKeyId, record);
            Save();
        }

        public bool ContainsPreKey(uint preKeyId)
        {
            return PreKeyStore.ContainsPreKey(preKeyId);
        }

        public void RemovePreKey(uint preKeyId)
        {
            PreKeyStore.RemovePreKey(preKeyId);
            Save();
        }

        public SessionRecord LoadSession(SignalProtocolAddress address)
        {
            return SessionStore.LoadSession(address);
        }

        public List<uint> GetSubDeviceSessions(string name)
        {
            return SessionStore.GetSubDeviceSessions(name);
        }

        public void StoreSession(SignalProtocolAddress address, SessionRecord record)
        {
            SessionStore.StoreSession(address, record);
            Save();
        }

        public bool ContainsSession(SignalProtocolAddress address)
        {
            return SessionStore.ContainsSession(address);
        }

        public void DeleteSession(SignalProtocolAddress address)
        {
            SessionStore.DeleteSession(address);
            Save();
        }

        public void DeleteAllSessions(string name)
        {
            SessionStore.DeleteAllSessions(name);
            Save();
        }

        public SignedPreKeyRecord LoadSignedPreKey(uint signedPreKeyId)
        {
            return SignedPreKeyStore.LoadSignedPreKey(signedPreKeyId);
        }

        public List<SignedPreKeyRecord> LoadSignedPreKeys()
        {
            return SignedPreKeyStore.LoadSignedPreKeys();
        }

        public void StoreSignedPreKey(uint signedPreKeyId, SignedPreKeyRecord record)
        {
            SignedPreKeyStore.StoreSignedPreKey(signedPreKeyId, record);
            Save();
        }

        public bool ContainsSignedPreKey(uint signedPreKeyId)
        {
            return SignedPreKeyStore.ContainsSignedPreKey(signedPreKeyId);
        }

        public void RemoveSignedPreKey(uint signedPreKeyId)
        {
            SignedPreKeyStore.RemoveSignedPreKey(signedPreKeyId);
            Save();
        }
    }

    public class JsonPreKeyStore : PreKeyStore
    {
        [JsonProperty] private Dictionary<uint, byte[]> _Store = new Dictionary<uint, byte[]>();

        public bool ContainsPreKey(uint preKeyId)
        {
            lock (Store.Lock)
            {
                return _Store.ContainsKey(preKeyId);
            }
        }

        public PreKeyRecord LoadPreKey(uint preKeyId)
        {
            lock (Store.Lock)
            {
                if (_Store.ContainsKey(preKeyId))
                {
                    return new PreKeyRecord(_Store[preKeyId]);
                }
                throw new InvalidKeyException("no such PreKeyRecord");
            }
        }

        public void RemovePreKey(uint preKeyId)
        {
            lock (Store.Lock)
            {
                _Store.Remove(preKeyId);
            }
        }

        public void StorePreKey(uint preKeyId, PreKeyRecord record)
        {
            lock (Store.Lock)
            {
                _Store[preKeyId] = record.serialize();
                Store.Instance.Save();
            }
        }
    }

    public class JsonIdentityKeyStore : IdentityKeyStore
    {
        [JsonProperty] private IdentityKeyPair IdentityKeyPair { get; set; }
        [JsonProperty] private uint RegistrationId { get; set; }
        [JsonProperty] private Dictionary<string, List<IdentityKey>> _Store { get; set; } = new Dictionary<string, List<IdentityKey>>();

        public JsonIdentityKeyStore(IdentityKeyPair identityKey, uint registrationId)
        {
            this.IdentityKeyPair = identityKey;
            this.RegistrationId = registrationId;
        }

        public IdentityKeyPair GetIdentityKeyPair()
        {
            return IdentityKeyPair;
        }

        public uint GetLocalRegistrationId()
        {
            return RegistrationId;
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

        public bool SaveIdentity(SignalProtocolAddress address, IdentityKey identityKey) //TODO why bool
        {
            throw new NotImplementedException();
        }
    }

    public class JsonSessionStore : SessionStore
    {
        [JsonProperty] private Dictionary<string, Dictionary<uint, byte[]>> _Store = new Dictionary<string, Dictionary<uint, byte[]>>();

        public bool ContainsSession(SignalProtocolAddress address)
        {
            lock (Store.Lock)
            {
                if (_Store.ContainsKey(address.Name))
                {
                    if (_Store[address.Name].ContainsKey(address.DeviceId))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void DeleteAllSessions(string name)
        {
            lock (Store.Lock)
            {
                _Store.Remove(name);
                Store.Instance.Save();
            }
        }

        public void DeleteSession(SignalProtocolAddress address)
        {
            lock (Store.Lock)
            {
                if (_Store.ContainsKey(address.Name))
                {
                    _Store[address.Name].Remove(address.DeviceId);
                    Store.Instance.Save();
                }
            }
        }

        public List<uint> GetSubDeviceSessions(string name)
        {
            lock (Store.Lock)
            {
                List<uint> deviceIds = new List<uint>();
                if (_Store.ContainsKey(name))
                {
                    foreach (var session in _Store[name])
                    {
                        if (session.Key != SignalServiceAddress.DEFAULT_DEVICE_ID)
                        {
                            deviceIds.Add(session.Key);
                        }
                    }
                }
                return deviceIds;
            }
        }

        public SessionRecord LoadSession(SignalProtocolAddress address)
        {
            lock (Store.Lock)
            {
                if (ContainsSession(address))
                    return new SessionRecord(_Store[address.Name][address.DeviceId]);
                else
                    return new SessionRecord();
            }
        }

        public void StoreSession(SignalProtocolAddress address, SessionRecord record)
        {
            lock (Store.Lock)
            {
                if (!_Store.ContainsKey(address.Name))
                {
                    _Store[address.Name] = new Dictionary<uint, byte[]>();
                }
                _Store[address.Name][address.DeviceId] = record.serialize();
                Store.Instance.Save();
            }
        }
    }

    public class JsonSignedPreKeyStore : SignedPreKeyStore
    {
        [JsonProperty] private Dictionary<uint, byte[]> _Store = new Dictionary<uint, byte[]>();

        public bool ContainsSignedPreKey(uint signedPreKeyId)
        {
            lock (Store.Lock)
            {
                return _Store.ContainsKey(signedPreKeyId);
            }
        }

        public SignedPreKeyRecord LoadSignedPreKey(uint signedPreKeyId)
        {
            lock (Store.Lock)
            {
                if (_Store.ContainsKey(signedPreKeyId))
                {
                    return new SignedPreKeyRecord(_Store[signedPreKeyId]);
                }
                throw new InvalidKeyException();
            }
        }

        public List<SignedPreKeyRecord> LoadSignedPreKeys()
        {
            lock (Store.Lock)
            {
                List<SignedPreKeyRecord> preKeys = new List<SignedPreKeyRecord>();
                foreach (var key in _Store.Keys)
                {
                    preKeys.Add(new SignedPreKeyRecord(_Store[key]));
                }
                return preKeys;
            }
        }

        public void RemoveSignedPreKey(uint signedPreKeyId)
        {
            lock (Store.Lock)
            {
                _Store.Remove(signedPreKeyId);
                Store.Instance.Save();
            }
        }

        public void StoreSignedPreKey(uint signedPreKeyId, SignedPreKeyRecord record)
        {
            lock (Store.Lock)
            {
                _Store[signedPreKeyId] = record.serialize();
                Store.Instance.Save();
            }
        }
    }

    public class ByteArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(byte[]))
            {
                return true;
            }
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string json = (string)reader.Value;
            return Base64.decode(json);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value.GetType() == typeof(byte[]))
            {
                byte[] arr = (byte[])value;
                writer.WriteValue(Base64.encodeBytes(arr));
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public class IdentityKeyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(IdentityKey))
            {
                return true;
            }
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string json = (string)reader.Value;
            return new IdentityKey(Curve.decodePoint(Base64.decodeWithoutPadding(json), 0));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value.GetType() == typeof(IdentityKey))
            {
                IdentityKey ik = (IdentityKey)value;
                writer.WriteValue(Base64.encodeBytes(ik.serialize()));
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public class IdentityKeyPairConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(IdentityKeyPair))
            {
                return true;
            }
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string json = (string)reader.Value;
            return new IdentityKeyPair(Base64.decode(json));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value.GetType() == typeof(IdentityKeyPair))
            {
                IdentityKeyPair ik = (IdentityKeyPair)value;
                writer.WriteValue(Base64.encodeBytes(ik.serialize()));
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }
}
