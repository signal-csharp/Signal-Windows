using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Signal_Windows.Storage;

namespace Signal_Windows.Migrations
{
    [DbContext(typeof(SignalDBContext))]
    partial class SignalDBContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
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

                    b.Property<bool>("CanReceive");

                    b.Property<string>("Color");

                    b.Property<string>("Draft");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<string>("ThreadDisplayName");

                    b.Property<string>("ThreadId");

                    b.Property<uint>("Unread");

                    b.HasKey("Id");

                    b.ToTable("Contacts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalEarlyReceipt", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<long>("Timestamp");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.HasIndex("Username");

                    b.ToTable("EarlyReceipts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalGroup", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AvatarFile");

                    b.Property<bool>("CanReceive");

                    b.Property<string>("Draft");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<string>("ThreadDisplayName");

                    b.Property<string>("ThreadId");

                    b.Property<uint>("Unread");

                    b.HasKey("Id");

                    b.ToTable("Groups");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalIdentity", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("IdentityKey");

                    b.Property<string>("Username");

                    b.Property<int>("VerifiedStatus");

                    b.HasKey("Id");

                    b.HasIndex("Username");

                    b.ToTable("Identities");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("AttachmentsCount");

                    b.Property<ulong?>("AuthorId");

                    b.Property<long>("ComposedTimestamp");

                    b.Property<ulong?>("Contentrowid");

                    b.Property<uint>("DeviceId");

                    b.Property<uint>("ReadConfirmations");

                    b.Property<uint>("Receipts");

                    b.Property<long>("ReceivedTimestamp");

                    b.Property<int>("Status");

                    b.Property<string>("ThreadID");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.HasIndex("Contentrowid");

                    b.HasIndex("ThreadID");

                    b.ToTable("Messages");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessageContent", b =>
                {
                    b.Property<ulong>("rowid")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Content");

                    b.HasKey("rowid");

                    b.ToTable("Messages_fts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalPreKey", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.HasKey("Id");

                    b.ToTable("PreKeys");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalSession", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<string>("Session");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.ToTable("Sessions");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalSignedPreKey", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.HasKey("Id");

                    b.ToTable("SignedPreKeys");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalStore", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<string>("IdentityKeyPair");

                    b.Property<uint>("NextSignedPreKeyId");

                    b.Property<string>("Password");

                    b.Property<uint>("PreKeyIdOffset");

                    b.Property<bool>("Registered");

                    b.Property<uint>("RegistrationId");

                    b.Property<string>("SignalingKey");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.ToTable("Store");
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

                    b.HasOne("Signal_Windows.Models.SignalMessageContent", "Content")
                        .WithMany()
                        .HasForeignKey("Contentrowid");
                });
        }
    }
}