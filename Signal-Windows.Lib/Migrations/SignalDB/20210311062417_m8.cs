using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Signal_Windows.Migrations
{
    public partial class m8 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ThreadGuid",
                table: "Messages",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ThreadGuid",
                table: "SignalConversation",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CdnNumber",
                table: "Attachments",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "V3StorageId",
                table: "Attachments",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThreadGuid",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ThreadGuid",
                table: "SignalConversation");

            migrationBuilder.DropColumn(
                name: "CdnNumber",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "V3StorageId",
                table: "Attachments");
        }
    }
}
