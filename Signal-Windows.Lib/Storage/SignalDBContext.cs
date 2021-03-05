using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using libsignalservice;
using libsignalservice.messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;

namespace Signal_Windows.Storage
{
    public sealed class SignalDBContext : DbContext
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
            var messages = new List<SignalMessage>();
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    messages = ctx.Messages
                        .Where(m => m.ThreadId == thread.ThreadId)
                        .Include(m => m.Content)
                        .Include(m => m.Author)
                        .Include(m => m.Attachments)
                        .OrderBy(m => m.Id)
                        .Skip(startIndex)
                        .AsNoTracking()
                        .Take(count)
                        .ToList();
                }
            }
            Logger.LogTrace($"GetMessagesLocked() returning {messages.Count} messages");
            return messages;
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
            return set_mark ? m : null;
        }

        public static void UpdateMessageExpiresAt(SignalMessage message)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var m = ctx.Messages.Single(t => t.Id == message.Id);
                    m.ExpiresAt = message.ExpiresAt;
                    ctx.SaveChanges();
                }
            }
        }

        /// <summary>
        /// Gets messages older than the given timestamp.
        /// </summary>
        /// <param name="timestamp">Timestamp in millis</param>
        /// <returns>Expired messages</returns>
        public static List<SignalMessage> GetExpiredMessages(long timestampMillis)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var messages = ctx.Messages
                        .Where(m => m.ExpiresAt > 0)
                        .Where(m => m.ExpiresAt < timestampMillis)
                        .Include(m => m.Attachments)
                        .Include(m => m.Content)
                        .AsNoTracking()
                        .ToList();
                    return messages;
                }
            }
        }

        public static void DeleteMessage(SignalMessage message)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    ctx.Remove(message);
                    SignalConversation conversation = ctx.Contacts
                        .Where(c => c.ThreadId == message.ThreadId)
                        .Single();
                    conversation.MessagesCount -= 1;
                    conversation.LastMessage = null;
                    conversation.LastMessageId = null;
                    conversation.LastSeenMessage = null;
                    conversation.LastSeenMessageIndex = ctx.Messages
                        .Where(m => m.ThreadId == conversation.ThreadId)
                        .Count() - 1;

                    // also delete fts message
                    SignalMessageContent ftsMessage = ctx.Messages_fts.Where(m => m == message.Content)
                        .Single();
                    ctx.Remove(ftsMessage);
                    ctx.SaveChanges();
                }
            }
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

        public static void DeleteAttachment(SignalAttachment attachment)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    ctx.Remove(attachment);
                    ctx.SaveChanges();
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
                        .Skip((int)conversation.LastSeenMessageIndex - 1)
                        .Take(1)
                        .Single();
                    if (message.Id > currentLastSeenMessage.Id)
                    {
                        var diff = (uint)ctx.Messages
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
