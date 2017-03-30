using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Signal_Windows.Storage;

namespace Signal_Windows.Migrations
{
    [DbContext(typeof(SignalDBContext))]
    partial class SignalDBContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1");

            modelBuilder.Entity("Signal_Windows.Models.SignalAttachment", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ContentType");

                    b.Property<string>("FileName");

                    b.Property<byte[]>("Key");

                    b.Property<uint?>("MessageId");

                    b.Property<string>("Relay");

                    b.Property<uint>("Status");

                    b.Property<ulong>("StorageId");

                    b.HasKey("Id");

                    b.HasIndex("MessageId");

                    b.ToTable("Attachments");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalContact", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Color");

                    b.Property<string>("ContactDisplayName");

                    b.Property<long>("LastActiveTimestamp");

                    b.Property<string>("UserName");

                    b.HasKey("Id");

                    b.ToTable("Contacts");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalMessage", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("AttachmentsCount");

                    b.Property<uint?>("AuthorId");

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

                    b.ToTable("Messages");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalAttachment", b =>
                {
                    b.HasOne("Signal_Windows.Models.SignalMessage", "Message")
                        .WithMany("Attachments")
                        .HasForeignKey("MessageId");
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
