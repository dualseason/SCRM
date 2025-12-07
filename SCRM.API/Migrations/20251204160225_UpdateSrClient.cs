using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSrClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sr_clients_wechat_accounts_accountId",
                table: "sr_clients");

            migrationBuilder.DropIndex(
                name: "IX_sr_clients_accountId",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "accountId",
                table: "sr_clients");

            migrationBuilder.AddColumn<string>(
                name: "SrClientuuid",
                table: "wechat_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip",
                table: "sr_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isOnline",
                table: "sr_clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "lastLoginAt",
                table: "sr_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "sr_clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "ip",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "isOnline",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "lastLoginAt",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "status",
                table: "sr_clients");

            migrationBuilder.AddColumn<long>(
                name: "accountId",
                table: "sr_clients",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_sr_clients_accountId",
                table: "sr_clients",
                column: "accountId");

            migrationBuilder.AddForeignKey(
                name: "FK_sr_clients_wechat_accounts_accountId",
                table: "sr_clients",
                column: "accountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
