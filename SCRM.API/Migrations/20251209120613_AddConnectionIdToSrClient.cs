using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionIdToSrClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wechat_accounts_sr_clients_SrClientuuid",
                table: "wechat_accounts");

            migrationBuilder.DropIndex(
                name: "IX_wechat_accounts_SrClientuuid",
                table: "wechat_accounts");

            migrationBuilder.DropColumn(
                name: "SrClientuuid",
                table: "wechat_accounts");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionId",
                table: "sr_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RedPackets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LikerNickname",
                table: "MomentsLikes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "Contacts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_wechat_accounts_client_uuid",
                table: "wechat_accounts",
                column: "client_uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_wechat_accounts_sr_clients_client_uuid",
                table: "wechat_accounts",
                column: "client_uuid",
                principalTable: "sr_clients",
                principalColumn: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wechat_accounts_sr_clients_client_uuid",
                table: "wechat_accounts");

            migrationBuilder.DropIndex(
                name: "IX_wechat_accounts_client_uuid",
                table: "wechat_accounts");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RedPackets");

            migrationBuilder.DropColumn(
                name: "LikerNickname",
                table: "MomentsLikes");

            migrationBuilder.AddColumn<string>(
                name: "SrClientuuid",
                table: "wechat_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "Contacts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_wechat_accounts_SrClientuuid",
                table: "wechat_accounts",
                column: "SrClientuuid");

            migrationBuilder.AddForeignKey(
                name: "FK_wechat_accounts_sr_clients_SrClientuuid",
                table: "wechat_accounts",
                column: "SrClientuuid",
                principalTable: "sr_clients",
                principalColumn: "uuid");
        }
    }
}
