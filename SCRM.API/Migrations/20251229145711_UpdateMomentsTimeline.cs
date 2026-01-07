using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMomentsTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkInfoJson",
                table: "MomentsTimeline",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "MomentsTimeline",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XmlContent",
                table: "MomentsTimeline",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkInfoJson",
                table: "MomentsTimeline");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "MomentsTimeline");

            migrationBuilder.DropColumn(
                name: "XmlContent",
                table: "MomentsTimeline");
        }
    }
}
