using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Signal_Windows.Storage;
using Signal_Windows.Models;

namespace Signal_Windows.Migrations
{
    [DbContext(typeof(SignalDBContext))]
    [Migration("20180203062229_m4")]
    partial class m4
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.4");

            modelBuilder.Entity("Signal_Windows.Models.GroupMembership", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("ContactId");

                    b.Property<long>("GroupId");

                    b.HasKey("Id");

                    b.HasIndex("ContactId");

                    b.HasIndex("GroupId");

                    b.ToTable("GroupMemberships");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalAttachment", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ContentType");

                    b.Property<byte[]>("Digest");

                    b.Property<string>("FileName");

                    b.Property<byte[]>("Key");

                    b.Property<long>("MessageId");

                    b.Property<string>("Relay");

                    b.Property<string>("SentFileName");

                    b.Property<long>("Size");

                    b.Property<int>("Status");

                    b.Property<ulong>("StorageId");

                    b.HasKey("Id");

                    b.HasIndex("MessageId");

                    b.ToTable("Attachments");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalConversation", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AvatarFile");

                    b.Property<bool>("CanReceive");

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<string>("Draft");

                    b.Property<uint>("ExpiresInSeconds");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<long?>("LastMessageId");

                    b.Property<long?>("LastSeenMessageId");

                    b.Property<long>("LastSeenMessageIndex");

                    b.Property<long>("MessagesCount");

                    b.Property<string>("ThreadDisplayName");

                    b.Property<string>("ThreadId");

                    b.Property<uint>("UnreadCount");

                    b.HasKey("Id");

                    b.HasIndex("LastMessageId");

                    b.HasIndex("LastSeenMessageId");

                    b.HasIndex("ThreadId");

                    b.ToTable("SignalConversation");

                    b.HasDiscriminator<string>("Discriminator").HasValue("SignalConversation");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalEarlyReceipt", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<long>("Timestamp");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.HasIndex("Timestamp");

                    b.HasIndex("Username");

                    b.ToTable("EarlyReceipts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("AttachmentsCount");

                    b.Property<long?>("AuthorId");

                    b.Property<long>("ComposedTimestamp");

                    b.Property<long?>("Contentrowid");

                    b.Property<uint>("DeviceId");

                    b.Property<int>("Direction");

                    b.Property<uint>("ExpiresAt");

                    b.Property<bool>("Read");

                    b.Property<uint>("Receipts");

                    b.Property<long>("ReceivedTimestamp");

                    b.Property<int>("Status");

                    b.Property<string>("ThreadId");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.HasIndex("Contentrowid");

                    b.HasIndex("ThreadId");

                    b.ToTable("Messages");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessageContent", b =>
                {
                    b.Property<long>("rowid")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Content");

                    b.HasKey("rowid");

                    b.ToTable("Messages_fts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalContact", b =>
                {
                    b.HasBaseType("Signal_Windows.Models.SignalConversation");

                    b.Property<string>("Color");

                    b.ToTable("SignalContact");

                    b.HasDiscriminator().HasValue("SignalContact");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalGroup", b =>
                {
                    b.HasBaseType("Signal_Windows.Models.SignalConversation");


                    b.ToTable("SignalGroup");

                    b.HasDiscriminator().HasValue("SignalGroup");
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

            modelBuilder.Entity("Signal_Windows.Models.SignalConversation", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalMessage", "LastMessage")
                        .WithMany()
                        .HasForeignKey("LastMessageId");

                    b.HasOne("Signal_Windows.Models.SignalMessage", "LastSeenMessage")
                        .WithMany()
                        .HasForeignKey("LastSeenMessageId");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalContact", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId");

                    b.HasOne("Signal_Windows.Models.SignalMessageContent", "Content")
                        .WithMany()
                        .HasForeignKey("Contentrowid");
                });
        }
    }
}
