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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Main.db");
        }

        public static void UpdateContact(SignalContact contact, bool flush)
        {
            lock (DBLock)
            {
                using (var db = new SignalDBContext())
                {
                    var c = db.Contacts.SingleOrDefaultAsync(b => b.UserName == contact.UserName).Result;
                    if (c == null)
                    {
                        c = new SignalContact()
                        {
                            Color = contact.Color,
                            UserName = contact.UserName,
                            ContactDisplayName = contact.ContactDisplayName
                        };
                        db.Contacts.Add(c);
                    }
                    else
                    {
                        c.Color = contact.Color;
                        c.UserName = contact.UserName;
                        c.ContactDisplayName = contact.ContactDisplayName;
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