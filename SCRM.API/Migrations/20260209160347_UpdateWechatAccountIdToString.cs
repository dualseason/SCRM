using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWechatAccountIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_wechat_accounts_wechat_account_id",
                table: "Contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_receiver_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_sender_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_wechat_accounts_AccountId",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_wechat_accounts_AssignedBy",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountaccountId",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_wechat_accounts",
                table: "wechat_accounts");

            migrationBuilder.DropIndex(
                name: "IX_users_WechatAccountaccountId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_Messages_receiver_id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_sender_id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_wechat_account_id_wxid",
                table: "Contacts");

            // DELAYED DROP: WechatAccountaccountId
            // migrationBuilder.DropColumn(
            //    name: "WechatAccountaccountId",
            //    table: "users");

            // DELAYED DROP: wechat_account_id
            // migrationBuilder.DropColumn(
            //    name: "wechat_account_id",
            //    table: "Contacts");

            // DELAYED DROP: account_id (moved to end)
            // migrationBuilder.DropColumn(
            //    name: "account_id",
            //    table: "wechat_accounts");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            // Step 1: Drop Identity (Keep bigint)
            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "users",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Step 2: Change type to string
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "WechatAccountwxid",
                table: "users",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedBy",
                table: "user_roles",
                type: "character varying(100)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "user_roles",
                type: "character varying(100)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "sender_wxid",
                table: "Messages",
                type: "character varying(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sender_id",
                table: "Messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "receiver_wxid",
                table: "Messages",
                type: "character varying(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "receiver_id",
                table: "Messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "account_id",
                table: "Messages",
                type: "character varying(100)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "wechat_account_id",
                table: "Conversations",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "owner_wxid",
                table: "Contacts",
                type: "character varying(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");


            // =================================================================================
            // DATA MIGRATION: Fix Foreign Keys by updating IDs from 'long' to 'string' (WxId)
            // =================================================================================

            // 1. Messages.account_id (Joined with wechat_accounts on old account_id)
            migrationBuilder.Sql(
                "UPDATE \"Messages\" m SET \"account_id\" = w.\"wxid\" " +
                "FROM \"wechat_accounts\" w " +
                "WHERE m.\"account_id\" = w.\"account_id\"::text AND w.\"wxid\" IS NOT NULL;");

            // 2. Conversations.wechat_account_id
             migrationBuilder.Sql(
                "UPDATE \"Conversations\" c SET \"wechat_account_id\" = w.\"wxid\" " +
                "FROM \"wechat_accounts\" w " +
                "WHERE c.\"wechat_account_id\" = w.\"account_id\"::text AND w.\"wxid\" IS NOT NULL;");

            // 3. UserRoles.AccountId & AssignedBy
            migrationBuilder.Sql(
                "UPDATE \"user_roles\" ur SET \"AccountId\" = w.\"wxid\" " +
                "FROM \"wechat_accounts\" w " +
                "WHERE ur.\"AccountId\" = w.\"account_id\"::text AND w.\"wxid\" IS NOT NULL;");

            // 4. Users.WechatAccountwxid (from WechatAccountaccountId)
            migrationBuilder.Sql(
                "UPDATE \"users\" u SET \"WechatAccountwxid\" = w.\"wxid\" " +
                "FROM \"wechat_accounts\" w " +
                "WHERE u.\"WechatAccountaccountId\" = w.\"account_id\" AND w.\"wxid\" IS NOT NULL;");
            
            // Clean up invalid references that didn't match (optional, preventing constraint failure)
            // migrationBuilder.Sql("DELETE FROM \"Messages\" WHERE \"account_id\" !~ '^wxid_';"); // Risky?

            // =================================================================================
            // EXECUTE DELAYED DROPS
            // =================================================================================
            migrationBuilder.DropColumn(name: "WechatAccountaccountId", table: "users");
            migrationBuilder.DropColumn(name: "wechat_account_id", table: "Contacts");
            migrationBuilder.DropColumn(name: "account_id", table: "wechat_accounts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_wechat_accounts",
                table: "wechat_accounts",
                column: "wxid");

            migrationBuilder.CreateIndex(
                name: "IX_users_WechatAccountwxid",
                table: "users",
                column: "WechatAccountwxid");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_receiver_wxid",
                table: "Messages",
                column: "receiver_wxid");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_sender_wxid",
                table: "Messages",
                column: "sender_wxid");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_owner_wxid_wxid",
                table: "Contacts",
                columns: new[] { "owner_wxid", "wxid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_wechat_accounts_owner_wxid",
                table: "Contacts",
                column: "owner_wxid",
                principalTable: "wechat_accounts",
                principalColumn: "wxid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages",
                column: "account_id",
                principalTable: "wechat_accounts",
                principalColumn: "wxid",
                onDelete: ReferentialAction.Cascade);

            // Removed FK_Messages_wechat_accounts_receiver_wxid (External contacts are not in wechat_accounts)
            // migrationBuilder.AddForeignKey(
            //    name: "FK_Messages_wechat_accounts_receiver_wxid",
            //    table: "Messages",
            //    column: "receiver_wxid",
            //    principalTable: "wechat_accounts",
            //    principalColumn: "wxid");

            // Removed FK_Messages_wechat_accounts_sender_wxid
            // migrationBuilder.AddForeignKey(
            //    name: "FK_Messages_wechat_accounts_sender_wxid",
            //    table: "Messages",
            //    column: "sender_wxid",
            //    principalTable: "wechat_accounts",
            //    principalColumn: "wxid");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_wechat_accounts_AccountId",
                table: "user_roles",
                column: "AccountId",
                principalTable: "wechat_accounts",
                principalColumn: "wxid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_wechat_accounts_AssignedBy",
                table: "user_roles",
                column: "AssignedBy",
                principalTable: "wechat_accounts",
                principalColumn: "wxid",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountwxid",
                table: "users",
                column: "WechatAccountwxid",
                principalTable: "wechat_accounts",
                principalColumn: "wxid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_wechat_accounts_owner_wxid",
                table: "Contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_receiver_wxid",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_sender_wxid",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_wechat_accounts_AccountId",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_wechat_accounts_AssignedBy",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountwxid",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_wechat_accounts",
                table: "wechat_accounts");

            migrationBuilder.DropIndex(
                name: "IX_users_WechatAccountwxid",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_Messages_receiver_wxid",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_sender_wxid",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_owner_wxid_wxid",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "WechatAccountwxid",
                table: "users");

            migrationBuilder.AddColumn<long>(
                name: "account_id",
                table: "wechat_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "users",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "WechatAccountaccountId",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "AssignedBy",
                table: "user_roles",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                table: "user_roles",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)");

            migrationBuilder.AlterColumn<string>(
                name: "sender_wxid",
                table: "Messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "sender_id",
                table: "Messages",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "receiver_wxid",
                table: "Messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "receiver_id",
                table: "Messages",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "account_id",
                table: "Messages",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)");

            migrationBuilder.AlterColumn<long>(
                name: "wechat_account_id",
                table: "Conversations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "owner_wxid",
                table: "Contacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)");

            migrationBuilder.AddColumn<long>(
                name: "wechat_account_id",
                table: "Contacts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_wechat_accounts",
                table: "wechat_accounts",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_WechatAccountaccountId",
                table: "users",
                column: "WechatAccountaccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_receiver_id",
                table: "Messages",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_sender_id",
                table: "Messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_wechat_account_id_wxid",
                table: "Contacts",
                columns: new[] { "wechat_account_id", "wxid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_wechat_accounts_wechat_account_id",
                table: "Contacts",
                column: "wechat_account_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_account_id",
                table: "Messages",
                column: "account_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_receiver_id",
                table: "Messages",
                column: "receiver_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_sender_id",
                table: "Messages",
                column: "sender_id",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_wechat_accounts_AccountId",
                table: "user_roles",
                column: "AccountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_wechat_accounts_AssignedBy",
                table: "user_roles",
                column: "AssignedBy",
                principalTable: "wechat_accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountaccountId",
                table: "users",
                column: "WechatAccountaccountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");
        }
    }
}
