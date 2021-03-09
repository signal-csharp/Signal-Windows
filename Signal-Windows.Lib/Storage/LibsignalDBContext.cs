using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using libsignal;
using libsignal.ecc;
using libsignal.state;
using libsignal.util;
using libsignalservice;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Signal_Windows.Lib;
using Signal_Windows.Models;

namespace Signal_Windows.Storage
{
    /// <summary>
    /// Persistent state representing the <see cref="SignalProtocolStore"/>.
    /// </summary>
    public sealed class LibsignalDBContext : DbContext
    {
        private static readonly object DBLock = new object();
        public DbSet<SignalIdentity> Identities { get; set; }
        public DbSet<SignalStore> Store { get; set; }
        public DbSet<SignalPreKey> PreKeys { get; set; }
        public DbSet<SignalSignedPreKey> SignedPreKeys { get; set; }
        public DbSet<SignalSession> Sessions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"Filename=..\LocalCache\Libsignal.db", x => x.SuppressForeignKeyEnforcement());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SignalIdentity>()
                .HasIndex(si => si.Username);

            modelBuilder.Entity<SignalSession>()
                .HasIndex(s => s.Username);

            modelBuilder.Entity<SignalSession>()
                .HasIndex(s => s.DeviceId);

            modelBuilder.Entity<SignalPreKey>()
                .HasIndex(pk => pk.Id);
        }

        public static void Migrate()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    if (ctx.Database.GetPendingMigrations().Count() > 0)
                    {
                        ctx.Database.Migrate();
                    }
                }
            }
        }

        public static void PurgeAccountData()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    ctx.Database.ExecuteSqlCommand("DELETE FROM Store;");
                    ctx.Database.ExecuteSqlCommand("DELETE FROM sqlite_sequence WHERE name = 'Store';");

                    ctx.Database.ExecuteSqlCommand("DELETE FROM SignedPreKeys;");
                    ctx.Database.ExecuteSqlCommand("DELETE FROM sqlite_sequence WHERE name = 'SignedPreKeys';");

                    ctx.Database.ExecuteSqlCommand("DELETE FROM PreKeys;");
                    ctx.Database.ExecuteSqlCommand("DELETE FROM sqlite_sequence WHERE name = 'PreKeys';");

                    ctx.Database.ExecuteSqlCommand("DELETE FROM Sessions;");
                    ctx.Database.ExecuteSqlCommand("DELETE FROM sqlite_sequence WHERE name = 'Sessions';");
                }
            }
        }

        #region Identities

        public static string GetIdentityLocked(string number)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var identity = ctx.Identities.LastOrDefault(i => i.Username == number);
                    if (identity == null)
                    {
                        return null;
                    }
                    return identity.IdentityKey;
                }
            }
        }

        private static LinkedList<SignalMessage> InsertIdentityChangedMessages(string number)
        {
            long now = Util.CurrentTimeMillis();
            LinkedList<SignalMessage> messages = new LinkedList<SignalMessage>();
            using (var ctx = new SignalDBContext())
            {
                SignalContact contact = SignalDBContext.GetSignalContactByThreadId(ctx, number);
                if (contact != null)
                {
                    string str = $"Your safety numbers with {contact.ThreadDisplayName} have changed.";
                    SignalMessage msg = new SignalMessage()
                    {
                        Author = contact,
                        ComposedTimestamp = now,
                        ReceivedTimestamp = now,
                        Direction = SignalMessageDirection.Incoming,
                        Type = SignalMessageType.IdentityKeyChange,
                        ThreadId = contact.ThreadId,
                        Content = new SignalMessageContent() { Content = str }
                    };
                    contact.LastMessage = msg;
                    contact.MessagesCount += 1;
                    contact.UnreadCount += 1;
                    ctx.Messages.Add(msg);
                    messages.AddLast(msg);
                    var groups = ctx.GroupMemberships
                        .Where(gm => gm.ContactId == contact.Id)
                        .Include(gm => gm.Group);
                    foreach (var gm in groups)
                    {
                        msg = new SignalMessage()
                        {
                            Author = contact,
                            ComposedTimestamp = now,
                            ReceivedTimestamp = now,
                            Direction = SignalMessageDirection.Incoming,
                            Type = SignalMessageType.IdentityKeyChange,
                            ThreadId = gm.Group.ThreadId,
                            Content = new SignalMessageContent() { Content = str }
                        };
                        gm.Group.LastMessage = msg;
                        gm.Group.MessagesCount += 1;
                        gm.Group.UnreadCount += 1;
                        ctx.Messages.Add(msg);
                        messages.AddLast(msg);
                    }
                }
                ctx.SaveChanges();
            }
            return messages;
        }

        public static async Task SaveIdentityLocked(SignalProtocolAddress address, string identity)
        {
            LinkedList<SignalMessage> messages = null;
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var old = ctx.Identities
                        .Where(i => i.Username == address.Name)
                        .FirstOrDefault(); //could contain stale data
                    if (old == null)
                    {
                        ctx.Identities.Add(new SignalIdentity()
                        {
                            IdentityKey = identity,
                            Username = address.Name,
                            VerifiedStatus = VerifiedStatus.Default
                        });
                    }
                    else if (old.IdentityKey != identity)
                    {
                        if (old.VerifiedStatus == VerifiedStatus.Verified)
                        {
                            old.VerifiedStatus = VerifiedStatus.Unverified;
                        }
                        old.IdentityKey = identity;
                        var oldSessions = ctx.Sessions
                            .Where(s => s.Username == address.Name);
                        foreach (var oldSession in oldSessions)
                        {
                            SessionRecord sessionRecord = new SessionRecord(Base64.Decode(oldSession.Session));
                            sessionRecord.archiveCurrentState();
                            oldSession.Session = Base64.EncodeBytes(sessionRecord.serialize());
                            SessionsCache[GetSessionCacheIndex(address.Name, oldSession.DeviceId)] = sessionRecord;
                        }
                        messages = InsertIdentityChangedMessages(address.Name);
                    }
                    ctx.SaveChanges();
                }
            }
            if (messages != null)
            {
                await SignalLibHandle.Instance.DispatchHandleIdentityKeyChange(messages);
            }
        }

        internal static IdentityKey GetIdentityKey(SignalProtocolAddress address)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    return new IdentityKey(Base64.Decode(ctx.Identities
                        .Where(identity => identity.Username == address.Name)
                        .Single().IdentityKey), 0);
                }
            }
        }
        #endregion Identities

        #region Account

        public static SignalStore GetSignalStore()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    return ctx.Store
                        .AsNoTracking()
                        .SingleOrDefault();
                }
            }
        }

        public static void SaveOrUpdateSignalStore(SignalStore store)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var old = ctx.Store.SingleOrDefault();
                    if (old != null)
                    {
                        old.DeviceId = store.DeviceId;
                        old.IdentityKeyPair = store.IdentityKeyPair;
                        old.NextSignedPreKeyId = store.NextSignedPreKeyId;
                        old.Password = store.Password;
                        old.PreKeyIdOffset = store.PreKeyIdOffset;
                        old.Registered = store.Registered;
                        old.RegistrationId = store.RegistrationId;
                        old.SignalingKey = store.SignalingKey;
                        old.Username = store.Username;
                    }
                    else
                    {
                        ctx.Store.Add(store);
                    }
                    ctx.SaveChanges();
                }
            }
        }

        public static void UpdatePreKeyIdOffset(uint preKeyIdOffset)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var s = ctx.Store.Single();
                    s.PreKeyIdOffset = preKeyIdOffset;
                    ctx.SaveChanges();
                }
            }
        }

        public static void UpdateNextSignedPreKeyId(uint nextSignedPreKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var s = ctx.Store.Single();
                    s.NextSignedPreKeyId = nextSignedPreKeyId;
                    ctx.SaveChanges();
                }
            }
        }

        public static IdentityKeyPair GetIdentityKeyPair()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var ikp = ctx.Store
                        .AsNoTracking()
                        .Single().IdentityKeyPair;
                    return new IdentityKeyPair(Base64.Decode(ikp));
                }
            }
        }

        public static uint GetLocalRegistrationId()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    return ctx.Store
                        .AsNoTracking()
                        .Single().RegistrationId;
                }
            }
        }

        #endregion Account

        #region Sessions
        public static void ClearSessionCache()
        {
            lock (DBLock)
            {
                SessionsCache.Clear();
            }
        }

        private static string GetSessionCacheIndex(string username, uint deviceid)
        {
            return username + @"\" + deviceid;
        }

        private static Dictionary<string, SessionRecord> SessionsCache = new Dictionary<string, SessionRecord>();

        public static SessionRecord LoadSessionLocked(SignalProtocolAddress address)
        {
            lock (DBLock)
            {
                string index = GetSessionCacheIndex(address.Name, address.DeviceId);
                if (SessionsCache.TryGetValue(index, out SessionRecord record))
                {
                    return record;
                }
                using (var ctx = new LibsignalDBContext())
                {
                    var session = ctx.Sessions
                        .Where(s => s.Username == address.Name && s.DeviceId == address.DeviceId)
                        .AsNoTracking()
                        .SingleOrDefault();
                    if (session != null)
                    {
                        record = new SessionRecord(Base64.Decode(session.Session));
                    }
                    else
                    {
                        record = new SessionRecord();
                    }
                    SessionsCache[index] = record;
                    return record;
                }
            }
        }

        public static List<uint> GetSubDeviceSessions(string name)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var sessions = ctx.Sessions
                        .Where(se => se.Username == name)
                        .AsNoTracking()
                        .ToList();
                    var s = new List<uint>();
                    foreach (var session in sessions)
                    {
                        if (session.DeviceId != SignalServiceAddress.DEFAULT_DEVICE_ID)
                        {
                            s.Add(session.DeviceId);
                        }
                    }
                    return s;
                }
            }
        }

        public static void StoreSession(SignalProtocolAddress address, SessionRecord record)
        {
            lock (DBLock)
            {
                string index = GetSessionCacheIndex(address.Name, address.DeviceId);
                using (var ctx = new LibsignalDBContext())
                {
                    var session = ctx.Sessions
                        .Where(s => s.DeviceId == address.DeviceId && s.Username == address.Name)
                        .SingleOrDefault();
                    if (session != null)
                    {
                        session.Session = Base64.EncodeBytes(record.serialize());
                    }
                    else
                    {
                        ctx.Sessions.Add(new SignalSession()
                        {
                            DeviceId = address.DeviceId,
                            Session = Base64.EncodeBytes(record.serialize()),
                            Username = address.Name
                        });
                    }
                    SessionsCache[index] = record;
                    ctx.SaveChanges();
                }
            }
        }

        public static bool ContainsSession(SignalProtocolAddress address)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var session = ctx.Sessions
                        .Where(s => s.Username == address.Name && s.DeviceId == address.DeviceId)
                        .SingleOrDefault();
                    if (session == null)
                        return false;

                    SessionRecord sessionRecord = new SessionRecord(Base64.Decode(session.Session));
                    return sessionRecord.getSessionState().hasSenderChain() &&
                        sessionRecord.getSessionState().getSessionVersion() == libsignal.protocol.CiphertextMessage.CURRENT_VERSION;
                }
            }
        }

        public static void DeleteSession(SignalProtocolAddress address)
        {
            lock (DBLock)
            {
                string index = GetSessionCacheIndex(address.Name, address.DeviceId);
                SessionsCache.Remove(index);
                using (var ctx = new LibsignalDBContext())
                {
                    var sessions = ctx.Sessions
                        .Where(s => s.Username == address.Name && s.DeviceId == address.DeviceId);
                    ctx.Sessions.RemoveRange(sessions);
                    ctx.SaveChanges();
                }
            }
        }

        public static void DeleteAllSessions(string name)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var sessions = ctx.Sessions
                        .Where(s => s.Username == name)
                        .ToList();
                    foreach (var session in sessions)
                    {
                        SessionsCache.Remove(GetSessionCacheIndex(name, session.DeviceId));
                    }
                    ctx.Sessions.RemoveRange(sessions);
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Sessions

        #region PreKeys

        public static PreKeyRecord LoadPreKey(uint preKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var pk = ctx.PreKeys
                        .Where(p => p.Id == preKeyId)
                        .AsNoTracking()
                        .Single();
                    return new PreKeyRecord(Base64.Decode(pk.Key));
                }
            }
        }

        public static void StorePreKey(uint preKeyId, PreKeyRecord record)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    ctx.PreKeys.Add(new SignalPreKey()
                    {
                        Id = preKeyId,
                        Key = Base64.EncodeBytes(record.serialize())
                    });
                    ctx.SaveChanges();
                }
            }
        }

        public static bool ContainsPreKey(uint preKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    return ctx.PreKeys
                        .Where(p => p.Id == preKeyId)
                        .AsNoTracking()
                        .SingleOrDefault() != null;
                }
            }
        }

        public static void RemovePreKey(uint preKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var preKey = ctx.PreKeys
                        .AsNoTracking()
                        .Where(b => b.Id == preKeyId)
                        .SingleOrDefault();
                    if (preKey != null)
                    {
                        ctx.PreKeys.Remove(preKey);
                        ctx.SaveChanges();
                    }
                }
            }
        }

        public static SignedPreKeyRecord LoadSignedPreKey(uint signedPreKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var preKeys = ctx.SignedPreKeys
                        .AsNoTracking()
                        .Where(b => b.Id == signedPreKeyId)
                        .Single();
                    return new SignedPreKeyRecord(Base64.Decode(preKeys.Key));
                }
            }
        }

        public static List<SignedPreKeyRecord> LoadSignedPreKeys()
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var preKeys = ctx.SignedPreKeys
                        .AsNoTracking()
                        .ToList();
                    var v = new List<SignedPreKeyRecord>();
                    foreach (var preKey in preKeys)
                    {
                        v.Add(new SignedPreKeyRecord(Base64.Decode(preKey.Key)));
                    }
                    return v;
                }
            }
        }

        public static void StoreSignedPreKey(uint signedPreKeyId, SignedPreKeyRecord record)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    ctx.SignedPreKeys.Add(new SignalSignedPreKey()
                    {
                        Id = signedPreKeyId,
                        Key = Base64.EncodeBytes(record.serialize())
                    });
                    ctx.SaveChanges();
                }
            }
        }

        public static bool ContainsSignedPreKey(uint signedPreKeyId)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var old = ctx.SignedPreKeys.Where(k => k.Id == signedPreKeyId).SingleOrDefault();
                    if (old != null)
                    {
                        return true;
                    }
                    return false;
                }
            }
        }

        public static void RemoveSignedPreKey(uint id)
        {
            lock (DBLock)
            {
                using (var ctx = new LibsignalDBContext())
                {
                    var old = ctx.SignedPreKeys.Where(k => k.Id == id).SingleOrDefault();
                    if (old != null)
                    {
                        ctx.SignedPreKeys.Remove(old);
                        ctx.SaveChanges();
                    }
                }
            }
        }

        public static async Task RefreshPreKeys(CancellationToken token, SignalServiceAccountManager accountManager) //TODO wrap in extra lock? enforce reload?
        {
            List<PreKeyRecord> oneTimePreKeys = GeneratePreKeys();
            SignedPreKeyRecord signedPreKeyRecord = GenerateSignedPreKey(GetIdentityKeyPair());
            await accountManager.SetPreKeys(token, GetIdentityKeyPair().getPublicKey(), signedPreKeyRecord, oneTimePreKeys);
        }

        private static List<PreKeyRecord> GeneratePreKeys()
        {
            List<PreKeyRecord> records = new List<PreKeyRecord>();
            for (uint i = 1; i < LibUtils.PREKEY_BATCH_SIZE; i++)
            {
                uint preKeyId = (SignalLibHandle.Instance.Store.PreKeyIdOffset + i) % Medium.MAX_VALUE;
                ECKeyPair keyPair = Curve.generateKeyPair();
                PreKeyRecord record = new PreKeyRecord(preKeyId, keyPair);

                StorePreKey(preKeyId, record);
                records.Add(record);
            }
            UpdatePreKeyIdOffset((SignalLibHandle.Instance.Store.PreKeyIdOffset + LibUtils.PREKEY_BATCH_SIZE + 1) % Medium.MAX_VALUE);
            return records;
        }

        private static PreKeyRecord GetOrGenerateLastResortPreKey()
        {
            if (ContainsPreKey(Medium.MAX_VALUE))
            {
                try
                {
                    return LoadPreKey(Medium.MAX_VALUE);
                }
                catch (InvalidKeyIdException)
                {
                    RemovePreKey(Medium.MAX_VALUE);
                }
            }
            ECKeyPair keyPair = Curve.generateKeyPair();
            PreKeyRecord record = new PreKeyRecord(Medium.MAX_VALUE, keyPair);
            StorePreKey(Medium.MAX_VALUE, record);
            return record;
        }

        private static SignedPreKeyRecord GenerateSignedPreKey(IdentityKeyPair identityKeyPair)
        {
            try
            {
                ECKeyPair keyPair = Curve.generateKeyPair();
                byte[] signature = Curve.calculateSignature(identityKeyPair.getPrivateKey(), keyPair.getPublicKey().serialize());
                SignedPreKeyRecord record = new SignedPreKeyRecord(SignalLibHandle.Instance.Store.NextSignedPreKeyId, (ulong)DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond, keyPair, signature);

                StoreSignedPreKey(SignalLibHandle.Instance.Store.NextSignedPreKeyId, record);
                UpdateNextSignedPreKeyId((SignalLibHandle.Instance.Store.NextSignedPreKeyId + 1) % Medium.MAX_VALUE);
                return record;
            }
            catch (InvalidKeyException e)
            {
                throw e;
            }
        }

        #endregion PreKeys
    }
}
