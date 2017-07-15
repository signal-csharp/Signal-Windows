using libsignalservice.messages;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Signal_Windows.Logging;
using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Signal_Windows.Storage
{
    public class SignalDBContext : DbContext
    {
        private static readonly object DBLock = new object();
        public DbSet<SignalContact> Contacts { get; set; }
        public DbSet<SignalMessage> Messages { get; set; }
        public DbSet<SignalAttachment> Attachments { get; set; }
        public DbSet<SignalGroup> Groups { get; set; }
        public DbSet<GroupMembership> GroupMemberships { get; set; }
        public DbSet<SignalMessageContent> Messages_fts { get; set; }
        public DbSet<SignalIdentity> Identities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Main.db", x => x.SuppressForeignKeyEnforcement());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SignalMessage>()
                .HasIndex(m => m.ThreadID);

            modelBuilder.Entity<SignalMessage>()
                .HasIndex(m => m.AuthorId);

            modelBuilder.Entity<SignalAttachment>()
                .HasIndex(a => a.MessageId);

            modelBuilder.Entity<GroupMembership>()
                .HasIndex(gm => gm.ContactId);

            modelBuilder.Entity<GroupMembership>()
                .HasIndex(gm => gm.GroupId);

            modelBuilder.Entity<SignalIdentity>()
                .HasIndex(si => si.Username);
        }

        public static void Migrate()
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    ctx.Database.Migrate();
                    var serviceProvider = ctx.GetInfrastructure<IServiceProvider>();
                    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    loggerFactory.AddProvider(new SqlLoggerProvider());
                }
            }
        }

        #region Messages

        internal static void SaveMessageLocked(SignalMessage message, bool incoming)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    if (incoming)
                    {
                        message.Author = ctx.Contacts.Single(b => b.ThreadId == message.Author.ThreadId);
                    }
                    ctx.Messages.Add(message);
                    ctx.SaveChanges();
                }
            }
        }

        public static List<SignalMessage> GetMessagesLocked(SignalThread thread, ThreadViewModel threadViewModel)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    return ctx.Messages
                        .Where(m => m.ThreadID == thread.ThreadId)
                        .Include(m => m.Content)
                        .Include(m => m.Author)
                        .Include(m => m.Attachments)
                        .AsNoTracking().ToList();
                }
            }
        }

        public static void UpdateMessageLocked(SignalMessage outgoingSignalMessage, MainPageViewModel mpvm)
        {
            SignalMessage m;
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    m = ctx.Messages.Single(t => t.ComposedTimestamp == outgoingSignalMessage.ComposedTimestamp && t.Author == null);
                    if (m != null)
                    {
                        m.Status = (uint)SignalMessageStatus.Confirmed;
                        ctx.SaveChanges();
                    }
                }
            }
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                mpvm.UIHandleSuccessfullSend(m);
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
                    m = ctx.Messages.SingleOrDefault(t => t.ComposedTimestamp == envelope.getTimestamp() && t.Author == null);
                    if (m != null)
                    {
                        m.Receipts++;
                        if (m.Status == (uint)SignalMessageStatus.Confirmed)
                        {
                            m.Status = (uint)SignalMessageStatus.Received;
                            set_mark = true;
                        }
                        ctx.SaveChanges();
                    }
                }
            }
            if (set_mark)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    mpvm.UIHandleReceiptReceived(m);
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

        #region Groups

        public static SignalGroup GetOrCreateGroupLocked(string groupId, MainPageViewModel mpvm)
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
                            LastActiveTimestamp = Util.CurrentTimeMillis(),
                            AvatarFile = null,
                            Unread = 1,
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

        public static SignalGroup InsertOrUpdateGroupLocked(string groupId, string displayname, string avatarfile, bool canReceive, MainPageViewModel mpvm)
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
                            LastActiveTimestamp = Util.CurrentTimeMillis(),
                            AvatarFile = avatarfile,
                            Unread = 1,
                            CanReceive = canReceive,
                            GroupMemberships = new List<GroupMembership>()
                        };
                        ctx.Add(dbgroup);
                    }
                    else
                    {
                        dbgroup.ThreadDisplayName = displayname;
                        dbgroup.LastActiveTimestamp = Util.CurrentTimeMillis();
                        dbgroup.AvatarFile = avatarfile;
                        dbgroup.Unread = 1;
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

        public static void InsertOrUpdateGroupMembershipLocked(ulong groupid, ulong memberid)
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
                        .Include(g => g.GroupMemberships)
                        .ThenInclude(gm => gm.Contact)
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
                    .AsNoTracking()
                    .ToList();
                }
            }
        }

        public static SignalContact GetOrCreateContactLocked(string username, MainPageViewModel mpvm)
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
                            CanReceive = true
                            //TODO pick random color
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
                        c.LastActiveTimestamp = contact.LastActiveTimestamp;
                        c.LastMessage = contact.LastMessage;
                        c.Unread = contact.Unread;
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

        #region Identities

        public static string GetIdentityLocked(string number)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
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

        public static void SaveIdentityLocked(string number, string identity)
        {
            lock (DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    var i = ctx.Identities.SingleOrDefault(id => id.Username == number && id.IdentityKey == identity);
                    if (i == null)
                    {
                        i = new SignalIdentity()
                        {
                            IdentityKey = identity,
                            Username = number,
                            VerifiedStatus = (uint)VerifiedStatus.Default
                        };
                        ctx.Identities.Add(i);
                    }
                    ctx.SaveChanges();
                }
            }
        }

        public static void UpdateIdentityLocked(string username, string identity, VerifiedStatus status, MainPageViewModel mpvm)
        {
            //TODO also hold store lock
            lock (DBLock)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await MainPage.NotifyNewIdentity(username);
                }).AsTask().Wait();
                using (var ctx = new SignalDBContext())
                {
                    var i = ctx.Identities.SingleOrDefault(id => id.Username == username);
                    if (i != null)
                    {
                        i.IdentityKey = identity;
                        i.VerifiedStatus = (uint)status;
                    }
                    ctx.SaveChanges();
                }
            }
        }

        #endregion Identities
    }
}
