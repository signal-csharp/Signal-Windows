using Microsoft.EntityFrameworkCore.Migrations;

namespace Signal_Windows.Migrations
{
    public partial class m1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AvatarFile = table.Column<string>(nullable: true),
                    CanReceive = table.Column<bool>(nullable: false),
                    Color = table.Column<string>(nullable: true),
                    Draft = table.Column<string>(nullable: true),
                    LastActiveTimestamp = table.Column<long>(nullable: false),
                    ThreadDisplayName = table.Column<string>(nullable: true),
                    ThreadId = table.Column<string>(nullable: true),
                    Unread = table.Column<uint>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EarlyReceipts",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<uint>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AvatarFile = table.Column<string>(nullable: true),
                    CanReceive = table.Column<bool>(nullable: false),
                    Draft = table.Column<string>(nullable: true),
                    LastActiveTimestamp = table.Column<long>(nullable: false),
                    ThreadDisplayName = table.Column<string>(nullable: true),
                    ThreadId = table.Column<string>(nullable: true),
                    Unread = table.Column<uint>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identities",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdentityKey = table.Column<string>(nullable: true),
                    Username = table.Column<string>(nullable: true),
                    VerifiedStatus = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identities", x => x.Id);
                });

            migrationBuilder.Sql(@"CREATE VIRTUAL TABLE Messages_fts USING fts5(Content);");
            /*
            migrationBuilder.CreateTable(
                name: "Messages_fts",
                columns: table => new
                {
                    rowid = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages_fts", x => x.rowid);
                });
            */

            migrationBuilder.CreateTable(
                name: "PreKeys",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<uint>(nullable: false),
                    Session = table.Column<string>(nullable: true),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignedPreKeys",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignedPreKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Store",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<uint>(nullable: false),
                    IdentityKeyPair = table.Column<string>(nullable: true),
                    NextSignedPreKeyId = table.Column<uint>(nullable: false),
                    Password = table.Column<string>(nullable: true),
                    PreKeyIdOffset = table.Column<uint>(nullable: false),
                    Registered = table.Column<bool>(nullable: false),
                    RegistrationId = table.Column<uint>(nullable: false),
                    SignalingKey = table.Column<string>(nullable: true),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Store", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContactId = table.Column<ulong>(nullable: false),
                    GroupId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttachmentsCount = table.Column<uint>(nullable: false),
                    AuthorId = table.Column<ulong>(nullable: true),
                    ComposedTimestamp = table.Column<long>(nullable: false),
                    Contentrowid = table.Column<ulong>(nullable: true),
                    DeviceId = table.Column<uint>(nullable: false),
                    ReadConfirmations = table.Column<uint>(nullable: false),
                    Receipts = table.Column<uint>(nullable: false),
                    ReceivedTimestamp = table.Column<long>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    ThreadID = table.Column<string>(nullable: true),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Contacts_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_fts_Contentrowid",
                        column: x => x.Contentrowid,
                        principalTable: "Messages_fts",
                        principalColumn: "rowid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContentType = table.Column<string>(nullable: true),
                    FileName = table.Column<string>(nullable: true),
                    Key = table.Column<byte[]>(nullable: true),
                    MessageId = table.Column<ulong>(nullable: false),
                    Relay = table.Column<string>(nullable: true),
                    SentFileName = table.Column<string>(nullable: true),
                    Status = table.Column<uint>(nullable: false),
                    StorageId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_ContactId",
                table: "GroupMemberships",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId",
                table: "GroupMemberships",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_MessageId",
                table: "Attachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyReceipts_DeviceId",
                table: "EarlyReceipts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyReceipts_Username",
                table: "EarlyReceipts",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Identities_Username",
                table: "Identities",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuthorId",
                table: "Messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Contentrowid",
                table: "Messages",
                column: "Contentrowid");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadID",
                table: "Messages",
                column: "ThreadID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupMemberships");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "EarlyReceipts");

            migrationBuilder.DropTable(
                name: "Identities");

            migrationBuilder.DropTable(
                name: "PreKeys");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "SignedPreKeys");

            migrationBuilder.DropTable(
                name: "Store");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Messages_fts");
        }
    }
}