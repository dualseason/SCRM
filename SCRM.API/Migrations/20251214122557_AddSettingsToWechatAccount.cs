using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsToWechatAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceAuthorizations_devices_DeviceId1",
                table: "DeviceAuthorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceCommands_devices_DeviceId1",
                table: "DeviceCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceHeartbeats_devices_DeviceId1",
                table: "DeviceHeartbeats");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceLocations_devices_DeviceId1",
                table: "DeviceLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceStatusLogs_devices_DeviceId1",
                table: "DeviceStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceVersionLogs_devices_DeviceId1",
                table: "DeviceVersionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerRedirects_devices_DeviceId1",
                table: "ServerRedirects");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemNotifications_devices_DeviceId",
                table: "SystemNotifications");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropIndex(
                name: "IX_SystemNotifications_DeviceId",
                table: "SystemNotifications");

            migrationBuilder.DropIndex(
                name: "IX_ServerRedirects_DeviceId1",
                table: "ServerRedirects");

            migrationBuilder.DropIndex(
                name: "IX_DeviceVersionLogs_DeviceId1",
                table: "DeviceVersionLogs");

            migrationBuilder.DropIndex(
                name: "IX_DeviceStatusLogs_DeviceId1",
                table: "DeviceStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_DeviceLocations_DeviceId1",
                table: "DeviceLocations");

            migrationBuilder.DropIndex(
                name: "IX_DeviceHeartbeats_DeviceId1",
                table: "DeviceHeartbeats");

            migrationBuilder.DropIndex(
                name: "IX_DeviceCommands_DeviceId1",
                table: "DeviceCommands");

            migrationBuilder.DropIndex(
                name: "IX_DeviceAuthorizations_DeviceId1",
                table: "DeviceAuthorizations");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SystemNotifications");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "ServerRedirects");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceVersionLogs");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceStatusLogs");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceLocations");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceHeartbeats");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceCommands");

            migrationBuilder.DropColumn(
                name: "DeviceId1",
                table: "DeviceAuthorizations");

            migrationBuilder.AddColumn<string>(
                name: "settings",
                table: "wechat_accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "settings",
                table: "wechat_accounts");

            migrationBuilder.AddColumn<long>(
                name: "DeviceId",
                table: "SystemNotifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "ServerRedirects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceVersionLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceStatusLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceLocations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceHeartbeats",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceCommands",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceId1",
                table: "DeviceAuthorizations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    DeviceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppVersion = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeviceBrand = table.Column<string>(type: "text", nullable: true),
                    DeviceModel = table.Column<string>(type: "text", nullable: true),
                    DeviceName = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<short>(type: "smallint", nullable: false),
                    DeviceUuid = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OsType = table.Column<string>(type: "text", nullable: true),
                    OsVersion = table.Column<string>(type: "text", nullable: true),
                    SdkVersion = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.DeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemNotifications_DeviceId",
                table: "SystemNotifications",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerRedirects_DeviceId1",
                table: "ServerRedirects",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceVersionLogs_DeviceId1",
                table: "DeviceVersionLogs",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceStatusLogs_DeviceId1",
                table: "DeviceStatusLogs",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocations_DeviceId1",
                table: "DeviceLocations",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHeartbeats_DeviceId1",
                table: "DeviceHeartbeats",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_DeviceId1",
                table: "DeviceCommands",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAuthorizations_DeviceId1",
                table: "DeviceAuthorizations",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_devices_IsDeleted",
                table: "devices",
                column: "IsDeleted");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceAuthorizations_devices_DeviceId1",
                table: "DeviceAuthorizations",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceCommands_devices_DeviceId1",
                table: "DeviceCommands",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceHeartbeats_devices_DeviceId1",
                table: "DeviceHeartbeats",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceLocations_devices_DeviceId1",
                table: "DeviceLocations",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceStatusLogs_devices_DeviceId1",
                table: "DeviceStatusLogs",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceVersionLogs_devices_DeviceId1",
                table: "DeviceVersionLogs",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerRedirects_devices_DeviceId1",
                table: "ServerRedirects",
                column: "DeviceId1",
                principalTable: "devices",
                principalColumn: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemNotifications_devices_DeviceId",
                table: "SystemNotifications",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "DeviceId");
        }
    }
}
