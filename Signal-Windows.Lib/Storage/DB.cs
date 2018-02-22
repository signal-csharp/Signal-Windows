using libsignal;
using libsignal.ecc;
using libsignal.state;
using libsignal.util;
using libsignalservice;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Signal_Windows.Storage
{
    public class LibsignalDBContext : DbContext
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

        public static LinkedList<SignalMessage> InsertIdentityChangedMessagesLocked(string number)
        {
            lock (DBLock)
            {
                return InsertIdentityChangedMessages(number);
            }
        }
        private static LinkedList<SignalMessage> InsertIdentityChangedMessages(string number)
        {
            long now = Util.CurrentTimeMillis();
            LinkedList<SignalMessage> messages = new LinkedList<SignalMessage>();
            using (var ctx = new SignalDBContext())
            {
                SignalContact contact = ctx.Contacts
                    .Where(c => c.ThreadId == number)
                    .SingleOrDefault();
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

        public static void SaveIdentityLocked(SignalProtocolAddress address, string identity)
        {
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
                        var childSessions = ctx.Sessions
                            .Where(s => s.Username == address.Name && s.DeviceId != address.DeviceId);
                        ctx.Sessions.RemoveRange(childSessions);
                        var messages = InsertIdentityChangedMessages(address.Name);
                        SignalLibHandle.Instance.DispatchHandleIdentityKeyChange(messages);
                    }
                    ctx.SaveChanges();
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
                    return new IdentityKeyPair(Base64.decode(ikp));
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
            lock(DBLock)
            {
                SessionsCache.Clear();
            }
        }

        private static string GetSessionCacheIndex(string username, uint deviceid)
        {
            return username + @"\" + deviceid;
        }

        private static Dictionary<string, SessionRecord> SessionsCache = new Dictionary<string, SessionRecord>();

        public static SessionRecord LoadSession(SignalProtocolAddress address)
        {
            lock (DBLock)
            {
                string index = GetSessionCacheIndex(address.Name, address.DeviceId);
                SessionRecord record;
                if (SessionsCache.TryGetValue(index, out record))
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
                        record = new SessionRecord(Base64.decode(session.Session));
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
                        session.Session = Base64.encodeBytes(record.serialize());
                    }
                    else
                    {
                        ctx.Sessions.Add(new SignalSession()
                        {
                            DeviceId = address.DeviceId,
                            Session = Base64.encodeBytes(record.serialize()),
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
                    return session != null;
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
                    return new PreKeyRecord(Base64.decode(pk.Key));
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
                        Key = Base64.encodeBytes(record.serialize())
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
                    return new SignedPreKeyRecord(Base64.decode(preKeys.Key));
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
                        v.Add(new SignedPreKeyRecord(Base64.decode(preKey.Key)));
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
                        Key = Base64.encodeBytes(record.serialize())
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

        public static void RefreshPreKeys(SignalServiceAccountManager accountManager) //TODO wrap in extra lock? enforce reload?
        {
            List<PreKeyRecord> oneTimePreKeys = GeneratePreKeys();
            SignedPreKeyRecord signedPreKeyRecord = generateSignedPreKey(GetIdentityKeyPair());
            accountManager.setPreKeys(GetIdentityKeyPair().getPublicKey(), signedPreKeyRecord, oneTimePreKeys);
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

        private static PreKeyRecord getOrGenerateLastResortPreKey()
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

        private static SignedPreKeyRecord generateSignedPreKey(IdentityKeyPair identityKeyPair)
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

    public class SignalDBContext : DbContext
    {
        private static readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalDBContext>();
        private static readonly object DBLock = new object();
        public DbSet<SignalContact> Contacts { get; set; }
        public DbSet<SignalMessage> Messages { get; set; }
        public DbSet<SignalAttachment> Attachments { get; set; }
        public DbSet<SignalGroup> Groups { get; set; }
        public DbSet<GroupMembership> GroupMemberships { get; set; }
        public DbSet<SignalMessageContent> Messages_fts { get; set; }
        public DbSet<SignalEarlyReceipt> EarlyReceipts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"Filename=..\LocalCache\Signal.db", x => x.SuppressForeignKeyEnforcement());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SignalMessage>()
                .HasIndex(m => m.ThreadId);

            modelBuilder.Entity<SignalMessage>()
                .HasIndex(m => m.AuthorId);

            modelBuilder.Entity<SignalAttachment>()
                .HasIndex(a => a.MessageId);

            modelBuilder.Entity<GroupMembership>()
                .HasIndex(gm => gm.ContactId);

            modelBuilder.Entity<GroupMembership>()
                .HasIndex(gm => gm.GroupId);

            modelBuilder.Entity<SignalEarlyReceipt>()
                .HasIndex(er => er.Username);

            modelBuilder.Entity<SignalEarlyReceipt>()
                .HasIndex(er => er.DeviceId);

            modelBuilder.Entity<SignalEarlyReceipt>()
                .HasIndex(er => er.Timestamp);

            modelBuilder.Entity<SignalConversation>()
                .HasOne(sc => sc.LastMessage);

            modelBuilder.Entity<SignalConversation>()
                .HasIndex(sc => sc.ThreadId);
        }

        public static void Migrate()
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    if (ctx.Database.GetPendingMigrations().Count() > 0)
                    {
                        ctx.Database.Migrate();
                    }
                    /*
                    var serviceProvider = ctx.GetInfrastructure<IServiceProvider>();
                    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    loggerFactory.AddProvider(new SqlLoggerProvider());
                    */
                }
            }
        }

        #region Messages

        public static void FailAllPendingMessages()
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var messages = ctx.Messages
                        .Where(m => m.Direction == SignalMessageDirection.Outgoing && m.Status == SignalMessageStatus.Pending).ToList();
                    messages.ForEach(m => m.Status = SignalMessageStatus.Failed_Unknown);
                    ctx.SaveChanges();
                }
            }
        }

        public static void SaveMessageLocked(SignalMessage message)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    long timestamp;
                    if (message.Direction == SignalMessageDirection.Synced)
                    {
                        var receipts = ctx.EarlyReceipts
                        .Where(er => er.Timestamp == message.ComposedTimestamp)
                        .ToList();

                        message.Receipts = (uint)receipts.Count;
                        ctx.EarlyReceipts.RemoveRange(receipts);
                        if (message.Receipts > 0)
                        {
                            message.Status = SignalMessageStatus.Received;
                        }
                    }
                    if (message.Author != null)
                    {
                        timestamp = message.ReceivedTimestamp;
                        message.Author = ctx.Contacts.Where(a => a.Id == message.Author.Id).Single();
                    }
                    else
                    {
                        timestamp = message.ComposedTimestamp;
                    }
                    if (!message.ThreadId.EndsWith("="))
                    {
                        var contact = ctx.Contacts
                            .Where(c => c.ThreadId == message.ThreadId)
                            .Single();
                        contact.LastActiveTimestamp = timestamp;
                        contact.LastMessage = message;
                        contact.MessagesCount += 1;
                        if (message.Author == null)
                        {
                            contact.UnreadCount = 0;
                            contact.LastSeenMessageIndex = contact.MessagesCount;
                        }
                        else
                        {
                            contact.UnreadCount += 1;
                        }
                    }
                    else
                    {
                        var group = ctx.Groups
                            .Where(c => c.ThreadId == message.ThreadId)
                            .Single();
                        message.ExpiresAt = group.ExpiresInSeconds;
                        group.LastActiveTimestamp = timestamp;
                        group.LastMessage = message;
                        group.MessagesCount += 1;
                        if (message.Author == null)
                        {
                            group.UnreadCount = 0;
                            group.LastSeenMessageIndex = group.MessagesCount;
                        }
                        else
                        {
                            group.UnreadCount += 1;
                        }
                    }
                    ctx.Messages.Add(message);
                    ctx.SaveChanges();
                }
            }
        }

        public static List<SignalMessageContainer> GetMessagesLocked(SignalConversation thread, int startIndex, int count)
        {
            Logger.LogTrace("GetMessagesLocked() skip {0} take {1}", startIndex, count);
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var messages = ctx.Messages
                        .Where(m => m.ThreadId == thread.ThreadId)
                        .Include(m => m.Content)
                        .Include(m => m.Author)
                        .Include(m => m.Attachments)
                        .OrderBy(m => m.Id)
                        .Skip(startIndex)
                        .Take(count);

                    var containers = new List<SignalMessageContainer>(count);
                    foreach (var message in messages)
                    {
                        containers.Add(new SignalMessageContainer(message, startIndex));
                        startIndex++;
                    }
                    return containers;
                }
            }
        }

        public static SignalMessage UpdateMessageStatus(SignalMessage outgoingSignalMessage)
        {
            SignalMessage m;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    m = ctx.Messages.Single(t => t.ComposedTimestamp == outgoingSignalMessage.ComposedTimestamp && t.Author == null);
                    if (m != null)
                    {
                        if (outgoingSignalMessage.Status == SignalMessageStatus.Confirmed)
                        {
                            if (m.Receipts > 0)
                            {
                                m.Status = SignalMessageStatus.Received;
                            }
                            else
                            {
                                m.Status = SignalMessageStatus.Confirmed;
                            }
                        }
                        else
                        {
                            m.Status = outgoingSignalMessage.Status;
                        }
                        ctx.SaveChanges();
                    }
                    return m;
                }
            }
        }

        public static SignalMessage IncreaseReceiptCountLocked(SignalServiceEnvelope envelope)
        {
            SignalMessage m;
            bool set_mark = false;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    m = ctx.Messages.SingleOrDefault(t => t.ComposedTimestamp == envelope.getTimestamp());
                    if (m != null)
                    {
                        m.Receipts++;
                        if (m.Status == SignalMessageStatus.Confirmed)
                        {
                            m.Status = SignalMessageStatus.Received;
                            set_mark = true;
                        }
                    }
                    else
                    {
                        ctx.EarlyReceipts.Add(new SignalEarlyReceipt()
                        {
                            DeviceId = (uint)envelope.getSourceDevice(),
                            Timestamp = envelope.getTimestamp(),
                            Username = envelope.getSource()
                        });
                    }
                    ctx.SaveChanges();
                }
            }
            return set_mark? m : null;
        }

        #endregion Messages

        #region Attachments

        public static SignalAttachment GetAttachmentByFileNameLocked(string fileName)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    return ctx.Attachments
                        .Where(a => a.FileName == fileName)
                        .FirstOrDefault();
                }
            }
        }

        internal static void UpdateAttachmentFileName(SignalAttachment attachment)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var savedAttachment = ctx.Attachments
                        .Where(a => a.Id == attachment.Id)
                        .First();
                    savedAttachment.FileName = attachment.FileName;
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Attachments

        #region Threads

        public static void UpdateExpiresInLocked(SignalConversation thread, uint exp)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    if (!thread.ThreadId.EndsWith("="))
                    {
                        var contact = ctx.Contacts
                            .Where(c => c.ThreadId == thread.ThreadId)
                            .SingleOrDefault();
                        if (contact != null)
                        {
                            contact.ExpiresInSeconds = exp;
                        }
                    }
                    else
                    {
                        var group = ctx.Groups
                            .Where(c => c.ThreadId == thread.ThreadId)
                            .SingleOrDefault();
                        if (group != null)
                        {
                            group.ExpiresInSeconds = exp;
                        }
                    }
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Threads

        #region Groups

        public static SignalGroup GetOrCreateGroupLocked(string groupId, long timestamp)
        {
            SignalGroup dbgroup;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    dbgroup = ctx.Groups
                        .Where(g => g.ThreadId == groupId)
                        .Include(g => g.GroupMemberships)
                        .ThenInclude(gm => gm.Contact)
                        .SingleOrDefault();
                    if (dbgroup == null)
                    {
                        dbgroup = new SignalGroup()
                        {
                            ThreadId = groupId,
                            ThreadDisplayName = "Unknown group",
                            LastActiveTimestamp = timestamp,
                            AvatarFile = null,
                            UnreadCount = 0,
                            CanReceive = false,
                            GroupMemberships = new List<GroupMembership>()
                        };
                        ctx.Add(dbgroup);
                        ctx.SaveChanges();
                    }
                }
            }
            return dbgroup;
        }

        public static SignalGroup InsertOrUpdateGroupLocked(string groupId, string displayname, string avatarfile, bool canReceive, long timestamp)
        {
            SignalGroup dbgroup;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    dbgroup = ctx.Groups
                        .Where(g => g.ThreadId == groupId)
                        .Include(g => g.GroupMemberships)
                        .ThenInclude(gm => gm.Contact)
                        .SingleOrDefault();
                    if (dbgroup == null)
                    {
                        dbgroup = new SignalGroup()
                        {
                            ThreadId = groupId,
                            ThreadDisplayName = displayname,
                            LastActiveTimestamp = timestamp,
                            AvatarFile = avatarfile,
                            UnreadCount = 0,
                            CanReceive = canReceive,
                            GroupMemberships = new List<GroupMembership>()
                        };
                        ctx.Add(dbgroup);
                    }
                    else
                    {
                        dbgroup.ThreadDisplayName = displayname;
                        dbgroup.LastActiveTimestamp = timestamp;
                        dbgroup.AvatarFile = avatarfile;
                        dbgroup.CanReceive = true;
                    }
                    ctx.SaveChanges();
                }
            }
            return dbgroup;
        }

        public static void InsertOrUpdateGroupMembershipLocked(long groupid, long memberid)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var old = ctx.GroupMemberships.Where(g => g.GroupId == groupid && g.ContactId == memberid).SingleOrDefault();
                    if (old == null)
                    {
                        ctx.GroupMemberships.Add(new GroupMembership()
                        {
                            ContactId = memberid,
                            GroupId = groupid
                        });
                        ctx.SaveChanges();
                    }
                }
            }
        }

        public static List<SignalGroup> GetAllGroupsLocked()
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    return ctx.Groups
                        .OrderByDescending(g => g.LastActiveTimestamp)
                        .Include(g => g.GroupMemberships)
                        .ThenInclude(gm => gm.Contact)
                        .Include(g => g.LastMessage)
                        .ThenInclude(m => m.Content)
                        .AsNoTracking()
                        .ToList();
                }
            }
        }

        #endregion Groups

        #region Contacts

        public static List<SignalContact> GetAllContactsLocked()
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    return ctx.Contacts
                        .OrderByDescending(c => c.LastActiveTimestamp)
                        .Include(g => g.LastMessage)
                        .ThenInclude(m => m.Content)
                        .AsNoTracking()
                        .ToList();
                }
            }
        }

        public static SignalContact GetOrCreateContactLocked(string username, long timestamp)
        {
            SignalContact contact;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    contact = ctx.Contacts
                        .Where(c => c.ThreadId == username)
                        .SingleOrDefault();
                    if (contact == null)
                    {
                        contact = new SignalContact()
                        {
                            ThreadId = username,
                            ThreadDisplayName = username,
                            CanReceive = true,
                            LastActiveTimestamp = timestamp,
                            Color = null //Utils.CalculateDefaultColor(username)
                        };
                        ctx.Contacts.Add(contact);
                        ctx.SaveChanges();
                    }
                }
            }
            return contact;
        }

        public static void InsertOrUpdateConversationLocked(SignalConversation conversation)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    if (conversation is SignalContact contact)
                    {
                        var c = ctx.Contacts.SingleOrDefault(b => b.ThreadId == conversation.ThreadId);
                        if (c == null)
                        {

                            ctx.Contacts.Add(contact);
                        }
                        else
                        {
                            c.Color = contact.Color;
                            c.ThreadId = conversation.ThreadId;
                            c.ThreadDisplayName = conversation.ThreadDisplayName;
                            c.CanReceive = conversation.CanReceive;
                            c.AvatarFile = conversation.AvatarFile;
                            c.Draft = conversation.Draft;
                            c.UnreadCount = conversation.UnreadCount;
                        }
                    }
                    else if (conversation is SignalGroup group)
                    {
                        var c = ctx.Groups.SingleOrDefault(b => b.ThreadId == conversation.ThreadId);
                        if (c == null)
                        {

                            ctx.Groups.Add(group);
                        }
                        else
                        {
                            c.ThreadId = conversation.ThreadId;
                            c.ThreadDisplayName = conversation.ThreadDisplayName;
                            c.CanReceive = conversation.CanReceive;
                            c.AvatarFile = conversation.AvatarFile;
                            c.Draft = conversation.Draft;
                            c.UnreadCount = conversation.UnreadCount;
                        }
                    }
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Contacts
    }
}