using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_Ids_And_Enrich_Moments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_AccountId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_AccountId",
                table: "Messages");

            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                table: "MomentsPosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkInfoJson",
                table: "MomentsPosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "MomentsPosts",
                type: "text",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_AccountId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_AccountId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ImagesJson",
                table: "MomentsPosts");

            migrationBuilder.DropColumn(
                name: "LinkInfoJson",
                table: "MomentsPosts");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "MomentsPosts");

            migrationBuilder.DropColumn(
                name: "AccountId1",
                table: "Messages");

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                table: "Messages",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "Contacts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AccountId",
                table: "Messages",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_AccountId",
                table: "Messages",
                column: "AccountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
