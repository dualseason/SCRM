using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFinderResultHistoryPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinderResultHistory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ownerKey = table.Column<string>(type: "text", nullable: false),
                    deviceUuid = table.Column<string>(type: "text", nullable: false),
                    weChatId = table.Column<string>(type: "text", nullable: false),
                    resultType = table.Column<string>(type: "text", nullable: false),
                    taskId = table.Column<long>(type: "bigint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    payloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    receivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinderResultHistory", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinderResultHistory_ownerKey_resultType_receivedAt",
                table: "FinderResultHistory",
                columns: new[] { "ownerKey", "resultType", "receivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinderResultHistory_ownerKey_resultType_taskId",
                table: "FinderResultHistory",
                columns: new[] { "ownerKey", "resultType", "taskId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinderResultHistory");
        }
    }
}
