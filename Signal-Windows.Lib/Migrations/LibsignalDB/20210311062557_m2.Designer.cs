using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Signal_Windows.Storage;
using Signal_Windows.Models;

namespace Signal_Windows.Migrations.LibsignalDB
{
    [DbContext(typeof(LibsignalDBContext))]
    [Migration("20210311062557_m2")]
    partial class m2
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.5");

            modelBuilder.Entity("Signal_Windows.Models.SignalIdentity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("IdentityKey");

                    b.Property<string>("Username");

                    b.Property<int>("VerifiedStatus");

                    b.HasKey("Id");

                    b.HasIndex("Username");

                    b.ToTable("Identities");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalPreKey", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.HasKey("Id");

                    b.HasIndex("Id");

                    b.ToTable("PreKeys");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalSession", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<string>("Session");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.HasIndex("Username");

                    b.ToTable("Sessions");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalSignedPreKey", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.HasKey("Id");

                    b.ToTable("SignedPreKeys");
                });

            modelBuilder.Entity("Signal_Windows.Models.SignalStore", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("DeviceId");

                    b.Property<string>("IdentityKeyPair");

                    b.Property<uint>("NextSignedPreKeyId");

                    b.Property<Guid?>("OwnGuid");

                    b.Property<string>("Password");

                    b.Property<uint>("PreKeyIdOffset");

                    b.Property<bool>("Registered");

                    b.Property<uint>("RegistrationId");

                    b.Property<string>("SignalingKey");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.ToTable("Store");
                });
        }
    }
}
