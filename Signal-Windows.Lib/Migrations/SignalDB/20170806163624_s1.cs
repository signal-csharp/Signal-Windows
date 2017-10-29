using Microsoft.EntityFrameworkCore.Migrations;

namespace Signal_Windows.Migrations
{
    public partial class s1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EarlyReceipts",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<uint>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyReceipts", x => x.Id);
                });

            migrationBuilder.Sql(@"CREATE VIRTUAL TABLE Messages_fts USING fts5(Content);");
            /*migrationBuilder.CreateTable(
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
                    Direction = table.Column<int>(nullable: false),
                    ExpiresAt = table.Column<uint>(nullable: false),
                    Read = table.Column<bool>(nullable: false),
                    Receipts = table.Column<uint>(nullable: false),
                    ReceivedTimestamp = table.Column<long>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    ThreadId = table.Column<string>(nullable: true),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    /*
                    table.ForeignKey(
                        name: "FK_Messages_Messages_fts_Contentrowid",
                        column: x => x.Contentrowid,
                        principalTable: "Messages_fts",
                        principalColumn: "rowid",
                        onDelete: ReferentialAction.Restrict);
                    */
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
                    Status = table.Column<int>(nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SignalConversation",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AvatarFile = table.Column<string>(nullable: true),
                    CanReceive = table.Column<bool>(nullable: false),
                    Discriminator = table.Column<string>(nullable: false),
                    Draft = table.Column<string>(nullable: true),
                    ExpiresInSeconds = table.Column<uint>(nullable: false),
                    LastActiveTimestamp = table.Column<long>(nullable: false),
                    LastMessageId = table.Column<ulong>(nullable: true),
                    LastSeenMessageId = table.Column<ulong>(nullable: true),
                    ThreadDisplayName = table.Column<string>(nullable: true),
                    ThreadId = table.Column<string>(nullable: true),
                    UnreadCount = table.Column<uint>(nullable: false),
                    Color = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalConversation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalConversation_Messages_LastMessageId",
                        column: x => x.LastMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignalConversation_Messages_LastSeenMessageId",
                        column: x => x.LastSeenMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_SignalConversation_LastMessageId",
                table: "SignalConversation",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalConversation_LastSeenMessageId",
                table: "SignalConversation",
                column: "LastSeenMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalConversation_ThreadId",
                table: "SignalConversation",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyReceipts_DeviceId",
                table: "EarlyReceipts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyReceipts_Timestamp",
                table: "EarlyReceipts",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyReceipts_Username",
                table: "EarlyReceipts",
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
                name: "IX_Messages_ThreadId",
                table: "Messages",
                column: "ThreadId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMemberships_SignalConversation_ContactId",
                table: "GroupMemberships",
                column: "ContactId",
                principalTable: "SignalConversation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMemberships_SignalConversation_GroupId",
                table: "GroupMemberships",
                column: "GroupId",
                principalTable: "SignalConversation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_SignalConversation_AuthorId",
                table: "Messages",
                column: "AuthorId",
                principalTable: "SignalConversation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_SignalConversation_AuthorId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "GroupMemberships");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "EarlyReceipts");

            migrationBuilder.DropTable(
                name: "SignalConversation");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Messages_fts");
        }
    }
}