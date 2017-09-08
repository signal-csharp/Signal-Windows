using libsignal;
using libsignal.ecc;
using libsignal.state;
using libsignal.util;
using libsignalservice;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Signal_Windows.Controls;
using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                    ctx.Database.Migrate();
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
                        Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                        {
                            await App.ViewModels.MainPageInstance.UIHandleIdentityKeyChange(address.Name);
                        }).AsTask().Wait();
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

        private static string GetSessionCacheIndex(string username, uint deviceid)
        {
            return username + @"\" + deviceid;
        }

        private static Dictionary<string, SessionRecord> SessionsCache = new Dictionary<string, SessionRecord>();

        public static SessionRecord LoadSession(SignalProtocolAddress address)
        {
            string index = GetSessionCacheIndex(address.Name, address.DeviceId);
            SessionRecord record;
            lock (DBLock)
            {
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
            string index = GetSessionCacheIndex(address.Name, address.DeviceId);
            lock (DBLock)
            {
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
            string index = GetSessionCacheIndex(address.Name, address.DeviceId);
            lock (DBLock)
            {
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
            PreKeyRecord lastResortKey = getOrGenerateLastResortPreKey();
            SignedPreKeyRecord signedPreKeyRecord = generateSignedPreKey(GetIdentityKeyPair());
            accountManager.setPreKeys(GetIdentityKeyPair().getPublicKey(), lastResortKey, signedPreKeyRecord, oneTimePreKeys);
        }

        private static List<PreKeyRecord> GeneratePreKeys()
        {
            List<PreKeyRecord> records = new List<PreKeyRecord>();
            for (uint i = 1; i < App.PREKEY_BATCH_SIZE; i++)
            {
                uint preKeyId = (App.Store.PreKeyIdOffset + i) % Medium.MAX_VALUE;
                ECKeyPair keyPair = Curve.generateKeyPair();
                PreKeyRecord record = new PreKeyRecord(preKeyId, keyPair);

                StorePreKey(preKeyId, record);
                records.Add(record);
            }
            UpdatePreKeyIdOffset((App.Store.PreKeyIdOffset + App.PREKEY_BATCH_SIZE + 1) % Medium.MAX_VALUE);
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
                SignedPreKeyRecord record = new SignedPreKeyRecord(App.Store.NextSignedPreKeyId, (ulong)DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond, keyPair, signature);

                StoreSignedPreKey(App.Store.NextSignedPreKeyId, record);
                UpdateNextSignedPreKeyId((App.Store.NextSignedPreKeyId + 1) % Medium.MAX_VALUE);
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
                    ctx.Database.Migrate();
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

        public static LinkedList<SignalMessage> InsertIdentityChangedMessages(string number)
        {
            long now = Util.CurrentTimeMillis();
            LinkedList<SignalMessage> messages = new LinkedList<SignalMessage>();
            lock (DBLock)
            {
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
                            ctx.Messages.Add(msg);
                            messages.AddLast(msg);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("InsertIdentityChangedMessages for non-existing contact!");
                    }
                    ctx.SaveChanges();
                }
            }
            return messages;
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
                    if (message.ThreadId.StartsWith("+"))
                    {
                        var contact = ctx.Contacts
                            .Where(c => c.ThreadId == message.ThreadId)
                            .Single();
                        contact.LastActiveTimestamp = timestamp;
                        contact.LastMessage = message;
                        contact.MessagesCount += 1;
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
                    }
                    ctx.Messages.Add(message);
                    ctx.SaveChanges();
                }
            }
        }

        public static List<SignalMessageContainer> GetMessagesLocked(SignalConversation thread, int startIndex, int count)
        {
            Debug.WriteLine($"GetMessagesLocked {thread.ThreadId} Skip({startIndex}) Take({count})");
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

        public static void UpdateMessageStatus(SignalMessage outgoingSignalMessage, MainPageViewModel mpvm)
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
                }
            }
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                mpvm.UIUpdateMessageBox(m);
            }).AsTask().Wait();
        }

        public static void IncreaseReceiptCountLocked(SignalServiceEnvelope envelope, MainPageViewModel mpvm)
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
            if (set_mark)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    mpvm.UIUpdateMessageBox(m);
                }).AsTask().Wait();
            }
        }

        #endregion Messages

        #region Attachments

        public static void UpdateAttachmentLocked(SignalAttachment sa)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    ctx.Attachments.Update(sa);
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
                    if (thread.ThreadId.StartsWith("+"))
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

        public static void UpdateConversationLocked(string threadId, uint unread, long? lastSeenMessageIndex)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var contact = ctx.Contacts
                        .Where(c => c.ThreadId == threadId)
                        .SingleOrDefault();
                    if (contact == null)
                    {
                        var group = ctx.Groups
                            .Where(g => g.ThreadId == threadId)
                            .Single();
                        group.UnreadCount = unread;
                        if (lastSeenMessageIndex != null)
                        {
                            group.LastSeenMessageIndex = lastSeenMessageIndex.Value;
                        }
                    }
                    else
                    {
                        contact.UnreadCount = unread;
                        if (lastSeenMessageIndex != null)
                        {
                            contact.LastSeenMessageIndex = lastSeenMessageIndex.Value;
                        }
                    }
                    ctx.SaveChanges();
                }
            }
        }

        public static SignalConversation ClearUnreadLocked(string threadId, long lastReadIndex)
        {
            lock (DBLock)
            {
                SignalConversation conversation;
                using (var ctx = new SignalDBContext())
                {
                    conversation = ctx.Contacts
                        .Where(c => c.ThreadId == threadId)
                        .SingleOrDefault();
                    if (conversation == null)
                    {
                        conversation = ctx.Groups
                            .Where(g => g.ThreadId == threadId)
                            .Single();
                        conversation.UnreadCount = 0;
                        conversation.LastSeenMessageIndex = lastReadIndex;
                    }
                    else
                    {
                        conversation.UnreadCount = 0;
                        conversation.LastSeenMessageIndex = lastReadIndex;
                    }
                    ctx.SaveChanges();
                }
                return conversation;
            }
        }

        #endregion Threads

        #region Groups

        public static SignalGroup GetOrCreateGroupLocked(string groupId, long timestamp, MainPageViewModel mpvm)
        {
            SignalGroup dbgroup;
            bool is_new = false;
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
                        is_new = true;
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
                if (is_new)
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.AddThread(dbgroup);
                    }).AsTask().Wait();
                }
            }
            return dbgroup;
        }

        public static SignalGroup InsertOrUpdateGroupLocked(string groupId, string displayname, string avatarfile, bool canReceive, long timestamp, MainPageViewModel mpvm)
        {
            SignalGroup dbgroup;
            bool is_new = false;
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
                        is_new = true;
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
                if (is_new)
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.AddThread(dbgroup);
                    }).AsTask().Wait();
                }
                else
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.UIUpdateThread(dbgroup);
                    }).AsTask().Wait();
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
                        .Include(g => g.LastSeenMessage)
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
                        .Include(g => g.LastSeenMessage)
                        .AsNoTracking()
                        .ToList();
                }
            }
        }

        public static SignalContact GetOrCreateContactLocked(string username, long timestamp, MainPageViewModel mpvm)
        {
            SignalContact contact;
            bool is_new = false;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    contact = ctx.Contacts
                        .Where(c => c.ThreadId == username)
                        .SingleOrDefault();
                    if (contact == null)
                    {
                        is_new = true;
                        contact = new SignalContact()
                        {
                            ThreadId = username,
                            ThreadDisplayName = username,
                            CanReceive = true,
                            LastActiveTimestamp = timestamp,
                            Color = Utils.Colors[Utils.CalculateDefaultColorIndex(username)]
                        };
                        ctx.Contacts.Add(contact);
                        ctx.SaveChanges();
                    }
                }
                if (is_new)
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.AddThread(contact);
                    }).AsTask().Wait();
                }
            }
            return contact;
        }

        public static void InsertOrUpdateContactLocked(SignalContact contact, MainPageViewModel mpvm)
        {
            bool is_new = false;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var c = ctx.Contacts.SingleOrDefault(b => b.ThreadId == contact.ThreadId);
                    if (c == null)
                    {
                        is_new = true;
                        ctx.Contacts.Add(contact);
                    }
                    else
                    {
                        c.Color = contact.Color;
                        c.ThreadId = contact.ThreadId;
                        c.ThreadDisplayName = contact.ThreadDisplayName;
                        c.CanReceive = contact.CanReceive;
                        c.AvatarFile = contact.AvatarFile;
                        c.Draft = contact.Draft;
                        c.UnreadCount = contact.UnreadCount;
                    }
                    ctx.SaveChanges();
                }
                if (is_new)
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.AddThread(contact);
                    }).AsTask().Wait();
                }
                else
                {
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        mpvm.UIUpdateThread(contact);
                    }).AsTask().Wait();
                }
            }
        }

        #endregion Contacts
    }
}