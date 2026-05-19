using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneSmsCallRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallLogRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerWxid = table.Column<string>(type: "text", nullable: false),
                    Imei = table.Column<string>(type: "text", nullable: false),
                    CallLogId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RawDate = table.Column<long>(type: "bigint", nullable: false),
                    CallTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RecordUrl = table.Column<string>(type: "text", nullable: false),
                    SimId = table.Column<int>(type: "integer", nullable: false),
                    BlockType = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallLogRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerWxid = table.Column<string>(type: "text", nullable: false),
                    Imei = table.Column<string>(type: "text", nullable: false),
                    SmsId = table.Column<int>(type: "integer", nullable: false),
                    ThreadId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RawDate = table.Column<long>(type: "bigint", nullable: false),
                    SmsTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    SimId = table.Column<int>(type: "integer", nullable: false),
                    BlockType = table.Column<int>(type: "integer", nullable: false),
                    SentNoticeType = table.Column<int>(type: "integer", nullable: false),
                    IsSentNoticeReceived = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentNoticeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogRecords_CallTime",
                table: "CallLogRecords",
                column: "CallTime");

            migrationBuilder.CreateIndex(
                name: "IX_CallLogRecords_OwnerWxid_Imei_CallLogId",
                table: "CallLogRecords",
                columns: new[] { "OwnerWxid", "Imei", "CallLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogRecords_OwnerWxid_Imei_RawDate_Number_Type",
                table: "CallLogRecords",
                columns: new[] { "OwnerWxid", "Imei", "RawDate", "Number", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsRecords_OwnerWxid_Imei_SmsId",
                table: "SmsRecords",
                columns: new[] { "OwnerWxid", "Imei", "SmsId" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsRecords_OwnerWxid_Imei_ThreadId_RawDate",
                table: "SmsRecords",
                columns: new[] { "OwnerWxid", "Imei", "ThreadId", "RawDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsRecords_SmsTime",
                table: "SmsRecords",
                column: "SmsTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallLogRecords");

            migrationBuilder.DropTable(
                name: "SmsRecords");
        }
    }
}
