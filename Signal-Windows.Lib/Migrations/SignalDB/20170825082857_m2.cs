using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Signal_Windows.Storage;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Signal_Windows.Migrations
{
    public partial class m2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MessagesCount",
                table: "SignalConversation",
                nullable: false,
                defaultValue: 0L);
            migrationBuilder.Sql(@"
                Update SignalConversation
                SET MessagesCount = (
                    SELECT Count()
                    FROM Messages
                    WHERE Messages.ThreadId == SignalConversation.ThreadId
                )");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessagesCount",
                table: "SignalConversation");
        }
    }
}
