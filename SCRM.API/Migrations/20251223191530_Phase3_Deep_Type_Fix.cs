using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_Deep_Type_Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "RedPackets",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "OfficialAccountSearchLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "OfficialAccounts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "MomentsPosts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "SnsId",
                table: "MomentsPosts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "MiniprogramSearchLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "MiniprogramAccounts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "MessageSyncLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "MassMessages",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "Groups",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "GroupMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberAvatar",
                table: "GroupMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberGender",
                table: "GroupMembers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "GroupMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "ContactTags",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "LabelId",
                table: "ContactTags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<long>(
                name: "WechatAccountId",
                table: "ContactGroups",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnsId",
                table: "MomentsPosts");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "MemberAvatar",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "MemberGender",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "LabelId",
                table: "ContactTags");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "RedPackets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "OfficialAccountSearchLogs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "OfficialAccounts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "MomentsPosts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "MiniprogramSearchLogs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "MiniprogramAccounts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "MessageSyncLogs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "MassMessages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "Groups",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "ContactTags",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "WechatAccountId",
                table: "ContactGroups",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
