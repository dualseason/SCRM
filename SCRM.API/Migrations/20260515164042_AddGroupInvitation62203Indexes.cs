using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupInvitation62203Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "InvitedJson",
                table: "GroupInvitations",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_WeChatId_ChatRoomId_UpdateTime",
                table: "GroupInvitations",
                columns: new[] { "WeChatId", "ChatRoomId", "UpdateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_WeChatId_InvitationStatus",
                table: "GroupInvitations",
                columns: new[] { "WeChatId", "InvitationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_WeChatId_MsgId",
                table: "GroupInvitations",
                columns: new[] { "WeChatId", "MsgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_WeChatId_ChatRoomId_UpdateTime",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_WeChatId_InvitationStatus",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_WeChatId_MsgId",
                table: "GroupInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "InvitedJson",
                table: "GroupInvitations",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "[]");
        }
    }
}
