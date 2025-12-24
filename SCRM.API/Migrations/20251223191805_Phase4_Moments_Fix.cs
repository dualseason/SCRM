using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_Moments_Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Manually handle cast from text to bigint
            migrationBuilder.Sql("ALTER TABLE \"MomentsTimeline\" ALTER COLUMN \"SnsId\" TYPE bigint USING \"SnsId\"::bigint");

            migrationBuilder.AddColumn<long>(
                name: "WechatAccountId",
                table: "MomentsTimeline",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WechatAccountId",
                table: "MomentsTimeline");

            migrationBuilder.AlterColumn<string>(
                name: "SnsId",
                table: "MomentsTimeline",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
