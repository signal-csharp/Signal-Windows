using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Signal_Windows.Storage;

namespace Signal_Windows.Migrations
{
    [DbContext(typeof(SignalDBContext))]
    [Migration("20170711104505_m")]
    partial class m
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.2");

            modelBuilder.Entity("Signal_Windows.Models.GroupMembership", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ContactId");

                    b.Property<ulong>("GroupId");

                    b.HasKey("Id");

                    b.HasIndex("ContactId");

                    b.HasIndex("GroupId");

                    b.ToTable("GroupMemberships");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalAttachment", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ContentType");

                    b.Property<string>("FileName");

                    b.Property<byte[]>("Key");

                    b.Property<ulong>("MessageId");

                    b.Property<string>("Relay");

                    b.Property<string>("SentFileName");

                    b.Property<uint>("Status");

                    b.Property<ulong>("StorageId");

                    b.HasKey("Id");

                    b.HasIndex("MessageId");

                    b.ToTable("Attachments");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalContact", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AvatarFile");

                    b.Property<string>("Color");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<string>("LastMessage");

                    b.Property<string>("ThreadDisplayName");

                    b.Property<string>("ThreadId");

                    b.Property<uint>("Unread");

                    b.HasKey("Id");

                    b.ToTable("Contacts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalGroup", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AvatarFile");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<string>("LastMessage");

                    b.Property<uint>("Status");

                    b.Property<string>("ThreadDisplayName");

                    b.Property<string>("ThreadId");

                    b.Property<uint>("Unread");

                    b.HasKey("Id");

                    b.ToTable("Groups");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("AttachmentsCount");

                    b.Property<ulong?>("AuthorId");

                    b.Property<long>("ComposedTimestamp");

                    b.Property<string>("Content");

                    b.Property<uint>("DeviceId");

                    b.Property<uint>("ReadConfirmations");

                    b.Property<uint>("Receipts");

                    b.Property<long>("ReceivedTimestamp");

                    b.Property<uint>("Status");

                    b.Property<string>("ThreadID");

                    b.Property<uint>("Type");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.HasIndex("ThreadID");

                    b.ToTable("Messages");
                });

            modelBuilder.Entity("Signal_Windows.Models.GroupMembership", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalContact", "Contact")
                        .WithMany("GroupMemberships")
                        .HasForeignKey("ContactId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Signal_Windows.Models.SignalGroup", "Group")
                        .WithMany("GroupMemberships")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalAttachment", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalMessage", "Message")
                        .WithMany("Attachments")
                        .HasForeignKey("MessageId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalContact", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId");
                });
        }
    }
}
