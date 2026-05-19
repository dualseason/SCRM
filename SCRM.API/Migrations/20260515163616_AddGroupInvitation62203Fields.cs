using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupInvitation62203Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatRoomId",
                table: "GroupInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InviteName",
                table: "GroupInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InvitedJson",
                table: "GroupInvitations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<long>(
                name: "MsgId",
                table: "GroupInvitations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MsgSvrId",
                table: "GroupInvitations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "GroupInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "GroupInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "TaskId",
                table: "GroupInvitations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UpdateTime",
                table: "GroupInvitations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "WeChatId",
                table: "GroupInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatRoomId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "InviteName",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "InvitedJson",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "MsgId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "MsgSvrId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "UpdateTime",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "WeChatId",
                table: "GroupInvitations");
        }
    }
}
