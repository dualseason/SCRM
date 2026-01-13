using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomConfigsToSrClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_configs",
                table: "sr_clients",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_wechat_accounts_wechat_account_id",
                table: "Contacts",
                column: "wechat_account_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_wechat_accounts_wechat_account_id",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "custom_configs",
                table: "sr_clients");
        }
    }
}
