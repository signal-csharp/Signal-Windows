using libsignal;
using libsignal.ecc;
using libsignal.state;
using libsignal.util;
using libsignalservice;
using libsignalservice.messages;
using libsignalservice.messages.multidevice;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                        foreach(var oldSession in oldSessions)
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
                    SaveMessage(ctx, message);
                    ctx.SaveChanges();
                }
            }
        }

        private static SignalConversation SaveMessage(SignalDBContext ctx, SignalMessage message)
        {
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
                message.Author = GetSignalContactByThreadId(ctx, message.Author.ThreadId);
            }
            SignalConversation conversation = GetSignalConversationByThreadId(ctx, message.ThreadId);
            conversation.LastActiveTimestamp = message.ComposedTimestamp;
            conversation.LastMessage = message;
            conversation.MessagesCount += 1;
            if (message.Author == null)
            {
                conversation.UnreadCount = 0;
                conversation.LastSeenMessageIndex = conversation.MessagesCount;
            }
            else
            {
                conversation.UnreadCount += 1;
            }
            ctx.Messages.Add(message);
            return conversation;
        }

        public static IEnumerable<SignalMessage> GetMessagesLocked(SignalConversation thread, int startIndex, int count)
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
                        .AsNoTracking()
                        .Take(count)
                        .ToList();
                    return messages;
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
                    m = ctx.Messages.SingleOrDefault(t => t.ComposedTimestamp == envelope.GetTimestamp());
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
                            DeviceId = (uint)envelope.GetSourceDevice(),
                            Timestamp = envelope.GetTimestamp(),
                            Username = envelope.GetSource()
                        });
                    }
                    ctx.SaveChanges();
                }
            }
            return set_mark? m : null;
        }

        #endregion Messages

        #region Attachments

        public static SignalAttachment GetAttachmentByGuidNameLocked(string guid)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    return ctx.Attachments
                        .Where(a => a.Guid == guid)
                        .FirstOrDefault();
                }
            }
        }

        internal static void UpdateAttachmentGuid(SignalAttachment attachment)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var savedAttachment = ctx.Attachments
                        .Where(a => a.Id == attachment.Id)
                        .First();
                    savedAttachment.Guid = attachment.Guid;
                    ctx.SaveChanges();
                }
            }
        }

        internal static void UpdateAttachmentStatus(SignalAttachment attachment)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var savedAttachment = ctx.Attachments
                        .Where(a => a.Id == attachment.Id)
                        .First();
                    savedAttachment.Status = attachment.Status;
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Attachments

        #region Conversations

        private static SignalConversation GetSignalConversationByThreadId(SignalDBContext ctx, string id)
        {
            if (!id.EndsWith("="))
            {
                return GetSignalContactByThreadId(ctx, id);
            }
            else
            {
                return GetSignalGroupByThreadId(ctx, id);
            }
        }

        internal static SignalContact GetSignalContactByThreadId(SignalDBContext ctx, string id)
        {
            return ctx.Contacts
                    .Where(c => c.ThreadId == id)
                    .SingleOrDefault();
        }

        private static SignalGroup GetSignalGroupByThreadId(SignalDBContext ctx, string id)
        {
            return ctx.Groups
                    .Where(c => c.ThreadId == id)
                    .SingleOrDefault();
        }

        public static void UpdateExpiresInLocked(SignalConversation thread)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var dbConversation = GetSignalConversationByThreadId(ctx, thread.ThreadId);
                    if (dbConversation != null)
                    {
                        dbConversation.ExpiresInSeconds = thread.ExpiresInSeconds;
                        ctx.SaveChanges();
                    }
                }
            }
        }

        internal static SignalConversation UpdateMessageRead(long timestamp)
        {
            SignalConversation conversation;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var message = ctx.Messages
                        .Where(m => m.ComposedTimestamp == timestamp)
                        .First(); //TODO care about early reads or messages with the same timestamp sometime
                    conversation = GetSignalConversationByThreadId(ctx, message.ThreadId);
                    var currentLastSeenMessage = ctx.Messages
                        .Where(m => m.ThreadId == conversation.ThreadId)
                        .Skip((int) conversation.LastSeenMessageIndex-1)
                        .Take(1)
                        .Single();
                    if (message.Id > currentLastSeenMessage.Id)
                    {
                        var diff = (uint) ctx.Messages
                            .Where(m => m.ThreadId == conversation.ThreadId && m.Id <= message.Id && m.Id > currentLastSeenMessage.Id)
                            .Count();
                        conversation.LastSeenMessageIndex += diff;
                        if (diff > conversation.UnreadCount)
                        {
                            throw new InvalidOperationException($"UpdateMessageRead encountered an inconsistent state: {diff} > {conversation.UnreadCount}");
                        }
                        conversation.UnreadCount -= diff;
                        ctx.SaveChanges();
                    }
                }
            }
            return conversation;
        }

        internal static async Task<List<SignalConversation>> InsertOrUpdateGroups(IList<(SignalGroup group, IList<string> members)> groups)
        {
            List<SignalConversation> refreshedGroups = new List<SignalConversation>();
            List<SignalContact> newContacts = new List<SignalContact>();
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    foreach (var (group, members) in groups)
                    {
                        try
                        {
                            var dbGroup = ctx.Groups
                            .Where(g => g.ThreadId == group.ThreadId)
                            .Include(g => g.GroupMemberships)
                            .Include(g => g.LastMessage)
                            .ThenInclude(m => m.Content)
                            .SingleOrDefault();
                            if (dbGroup != null)
                            {
                                dbGroup.GroupMemberships.Clear();
                                dbGroup.ThreadDisplayName = group.ThreadDisplayName;
                                dbGroup.CanReceive = group.CanReceive;
                                dbGroup.ExpiresInSeconds = group.ExpiresInSeconds;
                            }
                            else
                            {
                                dbGroup = group;
                                ctx.Groups.Add(dbGroup);
                            }
                            refreshedGroups.Add(dbGroup);
                            foreach (var member in members)
                            {
                                (var contact, var createdNew) = GetOrCreateContact(ctx, member, 0);
                                dbGroup.GroupMemberships.Add(new GroupMembership()
                                {
                                    Contact = contact,
                                    Group = dbGroup
                                });
                                if (createdNew)
                                {
                                    newContacts.Add(contact);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError("InsertOrUpdateGroups failed: {0}\n{1}", e.Message, e.StackTrace);
                        }
                    }
                    ctx.SaveChanges();
                }
            }
            foreach (var c in newContacts)
            {
                await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(c, null);
            }
            return refreshedGroups;
        }

        internal static IList<SignalConversation> InsertOrUpdateContacts(IList<SignalContact> contacts)
        {
            List<SignalConversation> refreshedContacts = new List<SignalConversation>();
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    foreach (var contact in contacts)
                    {
                        var dbContact = ctx.Contacts
                            .Where(c => c.ThreadId == contact.ThreadId)
                            .Include(c => c.LastMessage)
                            .ThenInclude(m => m.Content)
                            .SingleOrDefault();
                        if (dbContact != null)
                        {
                            refreshedContacts.Add(dbContact);
                            dbContact.ThreadDisplayName = contact.ThreadDisplayName;
                            dbContact.Color = contact.Color;
                            dbContact.CanReceive = contact.CanReceive;
                            dbContact.ExpiresInSeconds = contact.ExpiresInSeconds;
                        }
                        else
                        {
                            refreshedContacts.Add(contact);
                            ctx.Contacts.Add(contact);
                        }
                    }
                    ctx.SaveChanges();
                }
            }
            return refreshedContacts;
        }

        #endregion Threads

        #region Groups

        public static SignalConversation RemoveMemberFromGroup(string groupId, SignalContact member, SignalMessage quitMessage)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var dbgroup = ctx.Groups
                        .Where(g => g.ThreadId == groupId)
                        .Include(g => g.GroupMemberships)
                        .ThenInclude(gm => gm.Contact)
                        .Single();
                    dbgroup.GroupMemberships.RemoveAll(gm => gm.Contact.Id == member.Id);
                    if (member.ThreadId == SignalLibHandle.Instance.Store.Username)
                    {
                        dbgroup.CanReceive = false;
                    }
                    var conv = SaveMessage(ctx, quitMessage);
                    ctx.SaveChanges();
                    return conv;
                }
            }
        }

        public static async Task<SignalGroup> GetOrCreateGroupLocked(string groupId, long timestamp, bool notify = true)
        {
            SignalGroup dbgroup;
            bool createdNew = false;
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
                        createdNew = true;
                    }
                }
            }
            if (createdNew && notify)
            {
                await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(dbgroup, null);
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
                            ExpiresInSeconds = 0,
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

        public static async Task<SignalContact> GetOrCreateContactLocked(string username, long timestamp)
        {
            SignalContact contact;
            bool createdNew;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    (contact, createdNew) = GetOrCreateContact(ctx, username, timestamp);
                }
            }
            if (createdNew)
            {
                await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(contact, null);
            }
            return contact;
        }

        private static (SignalContact contact, bool createdNew) GetOrCreateContact(SignalDBContext ctx, string username, long timestamp)
        {
            bool createdNew = false;
            SignalContact contact = GetSignalContactByThreadId(ctx, username);
            if (contact == null)
            {
                contact = new SignalContact()
                {
                    ThreadId = username,
                    ThreadDisplayName = username,
                    CanReceive = true,
                    LastActiveTimestamp = timestamp,
                    Color = null
                };
                ctx.Contacts.Add(contact);
                ctx.SaveChanges();
                createdNew = true;
            }
            return (contact, createdNew);
        }

        public static void InsertOrUpdateConversationLocked(SignalConversation conversation)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var dbConversation = GetSignalConversationByThreadId(ctx, conversation.ThreadId);
                    if (dbConversation == null)
                    {
                        if (conversation is SignalContact dbContact)
                        {
                            ctx.Contacts.Add(dbContact);
                        }
                        else if (conversation is SignalGroup dbGroup)
                        {
                            ctx.Groups.Add(dbGroup);
                        }
                    }
                    else
                    {
                        dbConversation.ThreadId = conversation.ThreadId;
                        dbConversation.ThreadDisplayName = conversation.ThreadDisplayName;
                        dbConversation.CanReceive = conversation.CanReceive;
                        dbConversation.AvatarFile = conversation.AvatarFile;
                        dbConversation.Draft = conversation.Draft;
                        dbConversation.UnreadCount = conversation.UnreadCount;
                        if (dbConversation is SignalContact dbContact)
                        {
                            dbContact.Color = dbContact.Color;
                        }
                    }
                    ctx.SaveChanges();
                }
            }
        }

        public static void UpdateBlockStatus(SignalContact contact)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var c = GetSignalContactByThreadId(ctx, contact.ThreadId);
                    if (c == null)
                    {
                        throw new Exception("UpdateBlockStatus() failed: Could not find contact!");
                    }
                    c.Blocked = contact.Blocked;
                    ctx.SaveChanges();
                }
            }
        }
        #endregion Contacts
    }
}