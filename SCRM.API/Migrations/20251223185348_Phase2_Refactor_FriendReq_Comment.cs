using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_Refactor_FriendReq_Comment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReplyCommentId",
                table: "MomentsComments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WeChatCommentId",
                table: "MomentsComments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "FriendRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "FriendRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "FriendRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "FriendRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "FriendRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyCommentId",
                table: "MomentsComments");

            migrationBuilder.DropColumn(
                name: "WeChatCommentId",
                table: "MomentsComments");

            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FriendRequests");
        }
    }
}
