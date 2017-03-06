using Microsoft.EntityFrameworkCore;
using Signal_UWP.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Storage.DB
{
    public class SignalDBContext : DbContext
    {
        private static readonly Object obj = new Object();
        public DbSet<SignalContact> Contacts { get; set; }
        public DbSet<SignalMessage> Messages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Main.db");
        }

        public static void UpdateContact(SignalContact contact, bool flush)
        {
            lock(obj)
            {
                using(var db = new SignalDBContext())
                {
                    var c = db.Contacts.SingleOrDefaultAsync(b => b.E164Number == contact.E164Number).Result;
                    if(c == null)
                    {
                        c = new SignalContact()
                        {
                            Color = contact.Color,
                            E164Number = contact.E164Number,
                            ContactName = contact.ContactName
                        };
                        db.Contacts.Add(c);
                    } else {
                        c.Color = contact.Color;
                        c.E164Number = contact.E164Number;
                        c.ContactName = contact.ContactName;
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