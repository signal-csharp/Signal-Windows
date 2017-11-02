using Microsoft.EntityFrameworkCore.Migrations;

namespace Signal_Windows.Migrations.LibsignalDB
{
    public partial class ls1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "PreKeys",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
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
                    Id = table.Column<ulong>(nullable: false)
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
                    Id = table.Column<ulong>(nullable: false)
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
                    Id = table.Column<ulong>(nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Identities_Username",
                table: "Identities",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_PreKeys_Id",
                table: "PreKeys",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Username",
                table: "Sessions",
                column: "Username");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}