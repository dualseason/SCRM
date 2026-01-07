using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class FixSrClientColumnNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "sr_clients",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "tcpPort",
                table: "sr_clients",
                newName: "tcp_port");

            migrationBuilder.RenameColumn(
                name: "tcpHost",
                table: "sr_clients",
                newName: "tcp_host");

            migrationBuilder.RenameColumn(
                name: "lastLoginAt",
                table: "sr_clients",
                newName: "last_login_at");

            migrationBuilder.RenameColumn(
                name: "isOnline",
                table: "sr_clients",
                newName: "is_online");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "sr_clients",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConnectionId",
                table: "sr_clients",
                newName: "connection_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "sr_clients",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "tcp_port",
                table: "sr_clients",
                newName: "tcpPort");

            migrationBuilder.RenameColumn(
                name: "tcp_host",
                table: "sr_clients",
                newName: "tcpHost");

            migrationBuilder.RenameColumn(
                name: "last_login_at",
                table: "sr_clients",
                newName: "lastLoginAt");

            migrationBuilder.RenameColumn(
                name: "is_online",
                table: "sr_clients",
                newName: "isOnline");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "sr_clients",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "connection_id",
                table: "sr_clients",
                newName: "ConnectionId");
        }
    }
}
