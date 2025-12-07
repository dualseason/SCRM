using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SCRM.API.Models.DTOs;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSrClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_sr_clients",
                table: "sr_clients");

            migrationBuilder.DropIndex(
                name: "IX_sr_clients_machine_id",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "client_name",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "current_wechat_account_id",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "last_heartbeat",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "remark",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "version",
                table: "sr_clients");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "sr_clients",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "sr_clients",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "sr_clients",
                newName: "tcpPort");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sr_clients",
                newName: "accountId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updatedAt",
                table: "sr_clients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<long>(
                name: "accountId",
                table: "sr_clients",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "uuid",
                table: "sr_clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Device>(
                name: "device",
                table: "sr_clients",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "tcpHost",
                table: "sr_clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sr_clients",
                table: "sr_clients",
                column: "uuid");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sr_clients_wechat_accounts_accountId",
                table: "sr_clients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sr_clients",
                table: "sr_clients");

            migrationBuilder.DropIndex(
                name: "IX_sr_clients_accountId",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "uuid",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "device",
                table: "sr_clients");

            migrationBuilder.DropColumn(
                name: "tcpHost",
                table: "sr_clients");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "sr_clients",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "sr_clients",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "tcpPort",
                table: "sr_clients",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "accountId",
                table: "sr_clients",
                newName: "id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "sr_clients",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "sr_clients",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "client_name",
                table: "sr_clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "current_wechat_account_id",
                table: "sr_clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "sr_clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_heartbeat",
                table: "sr_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "machine_id",
                table: "sr_clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "remark",
                table: "sr_clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "version",
                table: "sr_clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_sr_clients",
                table: "sr_clients",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_sr_clients_machine_id",
                table: "sr_clients",
                column: "machine_id",
                unique: true);
        }
    }
}
