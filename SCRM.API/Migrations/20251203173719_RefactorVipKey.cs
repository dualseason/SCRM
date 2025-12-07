using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class RefactorVipKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vip_key",
                table: "wechat_accounts");

            migrationBuilder.CreateTable(
                name: "vip_keys",
                columns: table => new
                {
                    uuid = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    use_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    account_id = table.Column<long>(type: "bigint", nullable: true),
                    device_imei = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vip_keys", x => x.uuid);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vip_keys");

            migrationBuilder.AddColumn<string>(
                name: "vip_key",
                table: "wechat_accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
