using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCRM.API.Migrations
{
    /// <inheritdoc />
    public partial class FixColumnNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_ReceiverId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountAccountId",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "WechatAccountAccountId",
                table: "users",
                newName: "WechatAccountaccountId");

            migrationBuilder.RenameIndex(
                name: "IX_users_WechatAccountAccountId",
                table: "users",
                newName: "IX_users_WechatAccountaccountId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "roles",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "RoleName",
                table: "roles",
                newName: "roleName");

            migrationBuilder.RenameColumn(
                name: "RoleLevel",
                table: "roles",
                newName: "roleLevel");

            migrationBuilder.RenameColumn(
                name: "IsSystem",
                table: "roles",
                newName: "isSystem");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "roles",
                newName: "isDeleted");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "roles",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "roles",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "roles",
                newName: "roleId");

            migrationBuilder.RenameIndex(
                name: "IX_roles_RoleName",
                table: "roles",
                newName: "IX_roles_roleName");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "permissions",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "PermissionType",
                table: "permissions",
                newName: "permissionType");

            migrationBuilder.RenameColumn(
                name: "PermissionName",
                table: "permissions",
                newName: "permissionName");

            migrationBuilder.RenameColumn(
                name: "PermissionCode",
                table: "permissions",
                newName: "permissionCode");

            migrationBuilder.RenameColumn(
                name: "IsSystem",
                table: "permissions",
                newName: "isSystem");

            migrationBuilder.RenameColumn(
                name: "IsSensitive",
                table: "permissions",
                newName: "isSensitive");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "permissions",
                newName: "isDeleted");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "permissions",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "permissions",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "PermissionId",
                table: "permissions",
                newName: "permissionId");

            migrationBuilder.RenameIndex(
                name: "IX_permissions_PermissionCode",
                table: "permissions",
                newName: "IX_permissions_permissionCode");

            migrationBuilder.RenameColumn(
                name: "XmlContent",
                table: "MomentsTimeline",
                newName: "xmlContent");

            migrationBuilder.RenameColumn(
                name: "WechatAccountId",
                table: "MomentsTimeline",
                newName: "wechatAccountId");

            migrationBuilder.RenameColumn(
                name: "VideoUrl",
                table: "MomentsTimeline",
                newName: "videoUrl");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "MomentsTimeline",
                newName: "userName");

            migrationBuilder.RenameColumn(
                name: "SnsId",
                table: "MomentsTimeline",
                newName: "snsId");

            migrationBuilder.RenameColumn(
                name: "ReceivedAt",
                table: "MomentsTimeline",
                newName: "receivedAt");

            migrationBuilder.RenameColumn(
                name: "OwnerWxid",
                table: "MomentsTimeline",
                newName: "ownerWxid");

            migrationBuilder.RenameColumn(
                name: "NickName",
                table: "MomentsTimeline",
                newName: "nickName");

            migrationBuilder.RenameColumn(
                name: "LinkInfoJson",
                table: "MomentsTimeline",
                newName: "linkInfoJson");

            migrationBuilder.RenameColumn(
                name: "LikesJson",
                table: "MomentsTimeline",
                newName: "likesJson");

            migrationBuilder.RenameColumn(
                name: "ImagesJson",
                table: "MomentsTimeline",
                newName: "imagesJson");

            migrationBuilder.RenameColumn(
                name: "CreateTime",
                table: "MomentsTimeline",
                newName: "createTime");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "MomentsTimeline",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "CommentsJson",
                table: "MomentsTimeline",
                newName: "commentsJson");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "MomentsTimeline",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Direction",
                table: "Messages",
                newName: "direction");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Messages",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Messages",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SentAt",
                table: "Messages",
                newName: "sent_at");

            migrationBuilder.RenameColumn(
                name: "SenderWxid",
                table: "Messages",
                newName: "sender_wxid");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                table: "Messages",
                newName: "sender_id");

            migrationBuilder.RenameColumn(
                name: "SendStatus",
                table: "Messages",
                newName: "send_status");

            migrationBuilder.RenameColumn(
                name: "RevokedAt",
                table: "Messages",
                newName: "revoked_at");

            migrationBuilder.RenameColumn(
                name: "ReceiverWxid",
                table: "Messages",
                newName: "receiver_wxid");

            migrationBuilder.RenameColumn(
                name: "ReceiverId",
                table: "Messages",
                newName: "receiver_id");

            migrationBuilder.RenameColumn(
                name: "ReceivedAt",
                table: "Messages",
                newName: "received_at");

            migrationBuilder.RenameColumn(
                name: "ReadStatus",
                table: "Messages",
                newName: "read_status");

            migrationBuilder.RenameColumn(
                name: "ReadAt",
                table: "Messages",
                newName: "read_at");

            migrationBuilder.RenameColumn(
                name: "MsgSvrId",
                table: "Messages",
                newName: "msg_svr_id");

            migrationBuilder.RenameColumn(
                name: "MessageType",
                table: "Messages",
                newName: "message_type");

            migrationBuilder.RenameColumn(
                name: "LocalMessageId",
                table: "Messages",
                newName: "local_message_id");

            migrationBuilder.RenameColumn(
                name: "IsRevoked",
                table: "Messages",
                newName: "is_revoked");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Messages",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Messages",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConversationId",
                table: "Messages",
                newName: "conversation_id");

            migrationBuilder.RenameColumn(
                name: "ContentXml",
                table: "Messages",
                newName: "content_xml");

            migrationBuilder.RenameColumn(
                name: "ClientMsgId",
                table: "Messages",
                newName: "client_msg_id");

            migrationBuilder.RenameColumn(
                name: "ChatType",
                table: "Messages",
                newName: "chat_type");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "Messages",
                newName: "message_id");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_SenderId",
                table: "Messages",
                newName: "IX_Messages_sender_id");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_ReceiverId",
                table: "Messages",
                newName: "IX_Messages_receiver_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Groups",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WechatAccountId",
                table: "Groups",
                newName: "wechat_account_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Groups",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "OwnerWxid",
                table: "Groups",
                newName: "owner_wxid");

            migrationBuilder.RenameColumn(
                name: "MemberCount",
                table: "Groups",
                newName: "member_count");

            migrationBuilder.RenameColumn(
                name: "IsPinned",
                table: "Groups",
                newName: "is_pinned");

            migrationBuilder.RenameColumn(
                name: "IsMuted",
                table: "Groups",
                newName: "is_muted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Groups",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "GroupWxid",
                table: "Groups",
                newName: "group_wxid");

            migrationBuilder.RenameColumn(
                name: "GroupStatus",
                table: "Groups",
                newName: "group_status");

            migrationBuilder.RenameColumn(
                name: "GroupNotice",
                table: "Groups",
                newName: "group_notice");

            migrationBuilder.RenameColumn(
                name: "GroupName",
                table: "Groups",
                newName: "group_name");

            migrationBuilder.RenameColumn(
                name: "GroupDescription",
                table: "Groups",
                newName: "group_description");

            migrationBuilder.RenameColumn(
                name: "GroupAvatar",
                table: "Groups",
                newName: "group_avatar");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Groups",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Conversations",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WechatAccountId",
                table: "Conversations",
                newName: "wechat_account_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Conversations",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "UnreadCount",
                table: "Conversations",
                newName: "unread_count");

            migrationBuilder.RenameColumn(
                name: "MessageCount",
                table: "Conversations",
                newName: "message_count");

            migrationBuilder.RenameColumn(
                name: "LastMessageTime",
                table: "Conversations",
                newName: "last_message_time");

            migrationBuilder.RenameColumn(
                name: "LastMessageContent",
                table: "Conversations",
                newName: "last_message_content");

            migrationBuilder.RenameColumn(
                name: "IsPinned",
                table: "Conversations",
                newName: "is_pinned");

            migrationBuilder.RenameColumn(
                name: "IsMuted",
                table: "Conversations",
                newName: "is_muted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Conversations",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Conversations",
                newName: "display_name");

            migrationBuilder.RenameColumn(
                name: "DisplayAvatar",
                table: "Conversations",
                newName: "display_avatar");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Conversations",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConversationWxid",
                table: "Conversations",
                newName: "conversation_wxid");

            migrationBuilder.RenameColumn(
                name: "ConversationType",
                table: "Conversations",
                newName: "conversation_type");

            migrationBuilder.RenameColumn(
                name: "WechatAccountId",
                table: "ContactTags",
                newName: "wechatAccountId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "ContactTags",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "TagName",
                table: "ContactTags",
                newName: "tagName");

            migrationBuilder.RenameColumn(
                name: "TagDescription",
                table: "ContactTags",
                newName: "tagDescription");

            migrationBuilder.RenameColumn(
                name: "TagColor",
                table: "ContactTags",
                newName: "tagColor");

            migrationBuilder.RenameColumn(
                name: "LabelId",
                table: "ContactTags",
                newName: "labelId");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "ContactTags",
                newName: "isDeleted");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ContactTags",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ContactTags",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Wxid",
                table: "Contacts",
                newName: "wxid");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Contacts",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "Signature",
                table: "Contacts",
                newName: "signature");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "Contacts",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Province",
                table: "Contacts",
                newName: "province");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Contacts",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Nickname",
                table: "Contacts",
                newName: "nickname");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "Contacts",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Contacts",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Contacts",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Country",
                table: "Contacts",
                newName: "country");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "Contacts",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Avatar",
                table: "Contacts",
                newName: "avatar");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Contacts",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Contacts",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "LastInteractionTime",
                table: "Contacts",
                newName: "last_interaction_time");

            migrationBuilder.RenameColumn(
                name: "LabelIds",
                table: "Contacts",
                newName: "label_ids");

            migrationBuilder.RenameColumn(
                name: "IsStarred",
                table: "Contacts",
                newName: "is_starred");

            migrationBuilder.RenameColumn(
                name: "IsFriend",
                table: "Contacts",
                newName: "is_friend");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Contacts",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsBlocked",
                table: "Contacts",
                newName: "is_blocked");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Contacts",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ContactType",
                table: "Contacts",
                newName: "contact_type");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_wechat_account_id_Wxid",
                table: "Contacts",
                newName: "IX_Contacts_wechat_account_id_wxid");

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
                name: "FK_users_wechat_accounts_WechatAccountaccountId",
                table: "users",
                column: "WechatAccountaccountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_receiver_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_wechat_accounts_sender_id",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountaccountId",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "WechatAccountaccountId",
                table: "users",
                newName: "WechatAccountAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_users_WechatAccountaccountId",
                table: "users",
                newName: "IX_users_WechatAccountAccountId");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "roles",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "roleName",
                table: "roles",
                newName: "RoleName");

            migrationBuilder.RenameColumn(
                name: "roleLevel",
                table: "roles",
                newName: "RoleLevel");

            migrationBuilder.RenameColumn(
                name: "isSystem",
                table: "roles",
                newName: "IsSystem");

            migrationBuilder.RenameColumn(
                name: "isDeleted",
                table: "roles",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "roles",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "roles",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "roleId",
                table: "roles",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_roles_roleName",
                table: "roles",
                newName: "IX_roles_RoleName");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "permissions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "permissionType",
                table: "permissions",
                newName: "PermissionType");

            migrationBuilder.RenameColumn(
                name: "permissionName",
                table: "permissions",
                newName: "PermissionName");

            migrationBuilder.RenameColumn(
                name: "permissionCode",
                table: "permissions",
                newName: "PermissionCode");

            migrationBuilder.RenameColumn(
                name: "isSystem",
                table: "permissions",
                newName: "IsSystem");

            migrationBuilder.RenameColumn(
                name: "isSensitive",
                table: "permissions",
                newName: "IsSensitive");

            migrationBuilder.RenameColumn(
                name: "isDeleted",
                table: "permissions",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "permissions",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "permissions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "permissionId",
                table: "permissions",
                newName: "PermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_permissions_permissionCode",
                table: "permissions",
                newName: "IX_permissions_PermissionCode");

            migrationBuilder.RenameColumn(
                name: "xmlContent",
                table: "MomentsTimeline",
                newName: "XmlContent");

            migrationBuilder.RenameColumn(
                name: "wechatAccountId",
                table: "MomentsTimeline",
                newName: "WechatAccountId");

            migrationBuilder.RenameColumn(
                name: "videoUrl",
                table: "MomentsTimeline",
                newName: "VideoUrl");

            migrationBuilder.RenameColumn(
                name: "userName",
                table: "MomentsTimeline",
                newName: "UserName");

            migrationBuilder.RenameColumn(
                name: "snsId",
                table: "MomentsTimeline",
                newName: "SnsId");

            migrationBuilder.RenameColumn(
                name: "receivedAt",
                table: "MomentsTimeline",
                newName: "ReceivedAt");

            migrationBuilder.RenameColumn(
                name: "ownerWxid",
                table: "MomentsTimeline",
                newName: "OwnerWxid");

            migrationBuilder.RenameColumn(
                name: "nickName",
                table: "MomentsTimeline",
                newName: "NickName");

            migrationBuilder.RenameColumn(
                name: "linkInfoJson",
                table: "MomentsTimeline",
                newName: "LinkInfoJson");

            migrationBuilder.RenameColumn(
                name: "likesJson",
                table: "MomentsTimeline",
                newName: "LikesJson");

            migrationBuilder.RenameColumn(
                name: "imagesJson",
                table: "MomentsTimeline",
                newName: "ImagesJson");

            migrationBuilder.RenameColumn(
                name: "createTime",
                table: "MomentsTimeline",
                newName: "CreateTime");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "MomentsTimeline",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "commentsJson",
                table: "MomentsTimeline",
                newName: "CommentsJson");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "MomentsTimeline",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "direction",
                table: "Messages",
                newName: "Direction");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "Messages",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Messages",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "sent_at",
                table: "Messages",
                newName: "SentAt");

            migrationBuilder.RenameColumn(
                name: "sender_wxid",
                table: "Messages",
                newName: "SenderWxid");

            migrationBuilder.RenameColumn(
                name: "sender_id",
                table: "Messages",
                newName: "SenderId");

            migrationBuilder.RenameColumn(
                name: "send_status",
                table: "Messages",
                newName: "SendStatus");

            migrationBuilder.RenameColumn(
                name: "revoked_at",
                table: "Messages",
                newName: "RevokedAt");

            migrationBuilder.RenameColumn(
                name: "receiver_wxid",
                table: "Messages",
                newName: "ReceiverWxid");

            migrationBuilder.RenameColumn(
                name: "receiver_id",
                table: "Messages",
                newName: "ReceiverId");

            migrationBuilder.RenameColumn(
                name: "received_at",
                table: "Messages",
                newName: "ReceivedAt");

            migrationBuilder.RenameColumn(
                name: "read_status",
                table: "Messages",
                newName: "ReadStatus");

            migrationBuilder.RenameColumn(
                name: "read_at",
                table: "Messages",
                newName: "ReadAt");

            migrationBuilder.RenameColumn(
                name: "msg_svr_id",
                table: "Messages",
                newName: "MsgSvrId");

            migrationBuilder.RenameColumn(
                name: "message_type",
                table: "Messages",
                newName: "MessageType");

            migrationBuilder.RenameColumn(
                name: "local_message_id",
                table: "Messages",
                newName: "LocalMessageId");

            migrationBuilder.RenameColumn(
                name: "is_revoked",
                table: "Messages",
                newName: "IsRevoked");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "Messages",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Messages",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "conversation_id",
                table: "Messages",
                newName: "ConversationId");

            migrationBuilder.RenameColumn(
                name: "content_xml",
                table: "Messages",
                newName: "ContentXml");

            migrationBuilder.RenameColumn(
                name: "client_msg_id",
                table: "Messages",
                newName: "ClientMsgId");

            migrationBuilder.RenameColumn(
                name: "chat_type",
                table: "Messages",
                newName: "ChatType");

            migrationBuilder.RenameColumn(
                name: "message_id",
                table: "Messages",
                newName: "MessageId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_sender_id",
                table: "Messages",
                newName: "IX_Messages_SenderId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_receiver_id",
                table: "Messages",
                newName: "IX_Messages_ReceiverId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Groups",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "wechat_account_id",
                table: "Groups",
                newName: "WechatAccountId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Groups",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "owner_wxid",
                table: "Groups",
                newName: "OwnerWxid");

            migrationBuilder.RenameColumn(
                name: "member_count",
                table: "Groups",
                newName: "MemberCount");

            migrationBuilder.RenameColumn(
                name: "is_pinned",
                table: "Groups",
                newName: "IsPinned");

            migrationBuilder.RenameColumn(
                name: "is_muted",
                table: "Groups",
                newName: "IsMuted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "Groups",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "group_wxid",
                table: "Groups",
                newName: "GroupWxid");

            migrationBuilder.RenameColumn(
                name: "group_status",
                table: "Groups",
                newName: "GroupStatus");

            migrationBuilder.RenameColumn(
                name: "group_notice",
                table: "Groups",
                newName: "GroupNotice");

            migrationBuilder.RenameColumn(
                name: "group_name",
                table: "Groups",
                newName: "GroupName");

            migrationBuilder.RenameColumn(
                name: "group_description",
                table: "Groups",
                newName: "GroupDescription");

            migrationBuilder.RenameColumn(
                name: "group_avatar",
                table: "Groups",
                newName: "GroupAvatar");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Groups",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Conversations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "wechat_account_id",
                table: "Conversations",
                newName: "WechatAccountId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Conversations",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "unread_count",
                table: "Conversations",
                newName: "UnreadCount");

            migrationBuilder.RenameColumn(
                name: "message_count",
                table: "Conversations",
                newName: "MessageCount");

            migrationBuilder.RenameColumn(
                name: "last_message_time",
                table: "Conversations",
                newName: "LastMessageTime");

            migrationBuilder.RenameColumn(
                name: "last_message_content",
                table: "Conversations",
                newName: "LastMessageContent");

            migrationBuilder.RenameColumn(
                name: "is_pinned",
                table: "Conversations",
                newName: "IsPinned");

            migrationBuilder.RenameColumn(
                name: "is_muted",
                table: "Conversations",
                newName: "IsMuted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "Conversations",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "display_name",
                table: "Conversations",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "display_avatar",
                table: "Conversations",
                newName: "DisplayAvatar");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Conversations",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "conversation_wxid",
                table: "Conversations",
                newName: "ConversationWxid");

            migrationBuilder.RenameColumn(
                name: "conversation_type",
                table: "Conversations",
                newName: "ConversationType");

            migrationBuilder.RenameColumn(
                name: "wechatAccountId",
                table: "ContactTags",
                newName: "WechatAccountId");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "ContactTags",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tagName",
                table: "ContactTags",
                newName: "TagName");

            migrationBuilder.RenameColumn(
                name: "tagDescription",
                table: "ContactTags",
                newName: "TagDescription");

            migrationBuilder.RenameColumn(
                name: "tagColor",
                table: "ContactTags",
                newName: "TagColor");

            migrationBuilder.RenameColumn(
                name: "labelId",
                table: "ContactTags",
                newName: "LabelId");

            migrationBuilder.RenameColumn(
                name: "isDeleted",
                table: "ContactTags",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "ContactTags",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ContactTags",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "wxid",
                table: "Contacts",
                newName: "Wxid");

            migrationBuilder.RenameColumn(
                name: "source",
                table: "Contacts",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "signature",
                table: "Contacts",
                newName: "Signature");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "Contacts",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "province",
                table: "Contacts",
                newName: "Province");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "Contacts",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "nickname",
                table: "Contacts",
                newName: "Nickname");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "Contacts",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "Contacts",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Contacts",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "country",
                table: "Contacts",
                newName: "Country");

            migrationBuilder.RenameColumn(
                name: "city",
                table: "Contacts",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "avatar",
                table: "Contacts",
                newName: "Avatar");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Contacts",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Contacts",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "last_interaction_time",
                table: "Contacts",
                newName: "LastInteractionTime");

            migrationBuilder.RenameColumn(
                name: "label_ids",
                table: "Contacts",
                newName: "LabelIds");

            migrationBuilder.RenameColumn(
                name: "is_starred",
                table: "Contacts",
                newName: "IsStarred");

            migrationBuilder.RenameColumn(
                name: "is_friend",
                table: "Contacts",
                newName: "IsFriend");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "Contacts",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_blocked",
                table: "Contacts",
                newName: "IsBlocked");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Contacts",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "contact_type",
                table: "Contacts",
                newName: "ContactType");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_wechat_account_id_wxid",
                table: "Contacts",
                newName: "IX_Contacts_wechat_account_id_Wxid");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_ReceiverId",
                table: "Messages",
                column: "ReceiverId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_wechat_accounts_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_wechat_accounts_WechatAccountAccountId",
                table: "users",
                column: "WechatAccountAccountId",
                principalTable: "wechat_accounts",
                principalColumn: "account_id");
        }
    }
}
