using Microsoft.EntityFrameworkCore;
using Signal_Windows.Models;

namespace Signal_Windows.Storage
{
    public class SignalDBContext : DbContext
    {
        public static readonly object DBLock = new object();
        public DbSet<SignalContact> Contacts { get; set; }
        public DbSet<SignalMessage> Messages { get; set; }
        public DbSet<SignalAttachment> Attachments { get; set; }
        public DbSet<SignalGroup> Groups { get; set; }
        public DbSet<GroupMembership> GroupMemberships { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Main.db");
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
        }

        public static void UpdateContact(SignalContact contact, bool flush)
        {
            lock (DBLock)
            {
                using (var db = new SignalDBContext())
                {
                    var c = db.Contacts.SingleOrDefaultAsync(b => b.ThreadId == contact.ThreadId).Result;
                    if (c == null)
                    {
                        c = new SignalContact()
                        {
                            Color = contact.Color,
                            ThreadId = contact.ThreadId,
                            ThreadDisplayName = contact.ThreadDisplayName
                        };
                        db.Contacts.Add(c);
                    }
                    else
                    {
                        c.Color = contact.Color;
                        c.ThreadId = contact.ThreadId;
                        c.ThreadDisplayName = contact.ThreadDisplayName;
                    }
                    if (flush)
                    {
                        db.SaveChanges();
                    }
                }
            }
        }
    }
}