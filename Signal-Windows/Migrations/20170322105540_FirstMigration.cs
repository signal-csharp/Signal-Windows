using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Signal_Windows.Migrations
{
    public partial class FirstMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Color = table.Column<string>(nullable: true),
                    ContactDisplayName = table.Column<string>(nullable: true),
                    LastActiveTimestamp = table.Column<long>(nullable: false),
                    UserName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuthorId = table.Column<uint>(nullable: true),
                    ComposedTimestamp = table.Column<long>(nullable: false),
                    Content = table.Column<string>(nullable: true),
                    DeviceId = table.Column<uint>(nullable: false),
                    Receipts = table.Column<uint>(nullable: false),
                    ReceivedTimestamp = table.Column<long>(nullable: false),
                    Status = table.Column<uint>(nullable: false),
                    ThreadID = table.Column<string>(nullable: true),
                    Type = table.Column<uint>(nullable: false)
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuthorId",
                table: "Messages",
                column: "AuthorId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Contacts");
        }
    }
}
