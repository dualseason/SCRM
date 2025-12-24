using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_Unify_Long_Ids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_AccountId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_AccountId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "AccountId1",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Messages",
                newName: "account_id");

            migrationBuilder.RenameColumn(
                name: "WechatAccountId",
                table: "Contacts",
                newName: "wechat_account_id");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_WechatAccountId_Wxid",
                table: "Contacts",
                newName: "IX_Contacts_wechat_account_id_Wxid");

            migrationBuilder.AlterColumn<long>(
                name: "account_id",
                table: "Messages",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "FriendRequests",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "Conversations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "wechat_account_id",
                table: "Contacts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_account_id",
                table: "Messages",
                column: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages",
                column: "account_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_account_id",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "Messages",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "wechat_account_id",
                table: "Contacts",
                newName: "WechatAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_wechat_account_id_Wxid",
                table: "Contacts",
                newName: "IX_Contacts_WechatAccountId_Wxid");

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "Messages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "AccountId1",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "FriendRequests",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "Conversations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "Contacts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AccountId1",
                table: "Messages",
                column: "AccountId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_AccountId1",
                table: "Messages",
                column: "AccountId1",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
