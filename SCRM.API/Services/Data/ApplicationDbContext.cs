using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;


namespace SCRM.Services.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // ==================== 身份验证和授权实体集合 ====================
        public DbSet<LegacyWechatUser> LegacyWechatUsers { get; set; }
        public DbSet<WechatAccount> WechatAccounts { get; set; }
        public DbSet<VipKey> VipKeys { get; set; }
        public DbSet<Role> roles { get; set; }
        public DbSet<Permission> permissions { get; set; }
        public DbSet<UserRole> userRoles { get; set; }
        public DbSet<RolePermission> rolePermissions { get; set; }

        // ==================== 一、设备与账号管理 ====================


        // ==================== 二、好友管理 ====================
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactGroup> ContactGroups { get; set; }
        public DbSet<ContactTag> ContactTags { get; set; }
        public DbSet<ContactGroupRelation> ContactGroupRelations { get; set; }
        public DbSet<ContactTagRelation> ContactTagRelations { get; set; }
        public DbSet<ContactChangeLog> ContactChangeLogs { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<FriendDetectionLog> FriendDetectionLogs { get; set; }

        // ==================== 三、消息通信 ====================
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageMedia> MessageMedias { get; set; }
        public DbSet<MessageExtension> MessageExtensions { get; set; }
        public DbSet<MessageForward> MessageForwards { get; set; }
        public DbSet<MessageForwardDetail> MessageForwardDetails { get; set; }
        public DbSet<MessageRevocation> MessageRevocations { get; set; }
        public DbSet<MessageSyncLog> MessageSyncLogs { get; set; }
        public DbSet<VoiceToTextLog> VoiceToTextLogs { get; set; }

        // ==================== 四、群聊管理 ====================
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupAnnouncement> GroupAnnouncements { get; set; }
        public DbSet<GroupChangeLog> GroupChangeLogs { get; set; }
        public DbSet<GroupInvitation> GroupInvitations { get; set; }
        public DbSet<GroupMessageSyncLog> GroupMessageSyncLogs { get; set; }
        public DbSet<GroupQrcode> GroupQrcodes { get; set; }
        public DbSet<MassMessage> MassMessages { get; set; }
        public DbSet<MassMessageDetail> MassMessageDetails { get; set; }

        // ==================== 五、朋友圈 ====================
        public DbSet<MomentsPost> MomentsPosts { get; set; }
        public DbSet<MomentsTimeline> MomentsTimelines { get; set; }
        public DbSet<MomentsLike> MomentsLikes { get; set; }
        public DbSet<MomentsComment> MomentsComments { get; set; }
        public DbSet<FinderResultHistory> FinderResultHistories { get; set; }

        // ==================== 六、钱包与红包 ====================
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<RedPacket> RedPackets { get; set; }
        public DbSet<RedPacketRecord> RedPacketRecords { get; set; }

        // ==================== 七、公众号与小程序 ====================
        public DbSet<OfficialAccount> OfficialAccounts { get; set; }
        public DbSet<MiniprogramAccount> MiniprogramAccounts { get; set; }
        public DbSet<OfficialAccountSearchLog> OfficialAccountSearchLogs { get; set; }
        public DbSet<MiniprogramSearchLog> MiniprogramSearchLogs { get; set; }
        public DbSet<OfficialAccountMessage> OfficialAccountMessages { get; set; }
        public DbSet<MiniprogramMessage> MiniprogramMessages { get; set; }
        public DbSet<OfficialAccountSubscription> OfficialAccountSubscriptions { get; set; }
        public DbSet<MiniprogramAccessLog> MiniprogramAccessLogs { get; set; }
        public DbSet<OfficialAccountFollowLog> OfficialAccountFollowLogs { get; set; }
        public DbSet<MiniprogramFollowLog> MiniprogramFollowLogs { get; set; }

        // ==================== 八、会话管理 ====================
        public DbSet<Conversation> Conversations { get; set; }

        // ==================== 九、设备与手机操作 ====================
        public DbSet<ServerRedirect> ServerRedirects { get; set; }
        public DbSet<SmsRecord> SmsRecords { get; set; }
        public DbSet<CallLogRecord> CallLogRecords { get; set; }

        // ==================== 十、其他功能 ====================
        // ==================== 十、其他功能 ====================
        public DbSet<SystemNotification> SystemNotifications { get; set; }
        public DbSet<AppVersion> AppVersions { get; set; }
        public DbSet<SrClient> SrClients { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<SystemConfig> SystemConfigs { get; set; } // 新增配置表

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 LegacyWechatUser 实体
            modelBuilder.Entity<LegacyWechatUser>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.UserId);
                // ... (existing code) ...
            });

            // 配置 SystemConfig 索引
            modelBuilder.Entity<SystemConfig>(entity =>
            {
                entity.HasIndex(e => e.key).IsUnique(); // 键名唯一
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 SystemLog 索引
            modelBuilder.Entity<SystemLog>(entity =>
            {
                entity.HasIndex(e => e.createdAt); // 用于按时间查询
                entity.HasIndex(e => e.level);     // 用于按级别筛选
                entity.HasIndex(e => e.module);    // 用于按模块筛选
                entity.HasIndex(e => e.action);    // 用于按动作筛选
                entity.HasIndex(e => e.operatorId);// 用于查询某个人的操作记录
            });

            // 配置 Role 实体
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.roleId);
                entity.HasIndex(e => e.roleName);
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Permission 实体
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("permissions");
                entity.HasKey(e => e.permissionId);
                entity.HasIndex(e => e.permissionCode);
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 UserRole 关系
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(e => e.userRoleId);
                entity.HasIndex(e => new { e.accountId, e.roleId });
                entity.Property(e => e.assignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 配置外键关系
                entity.HasOne(e => e.account)
                      .WithMany()
                      .HasForeignKey(e => e.accountId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.role)
                      .WithMany(r => r.userRoles)
                      .HasForeignKey(e => e.roleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.assignedByAccount)
                      .WithMany()
                      .HasForeignKey(e => e.assignedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // 配置 RolePermission 关系
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("role_permissions");
                entity.HasKey(e => e.rolePermId);
                entity.HasIndex(e => new { e.roleId, e.permissionId });
                entity.Property(e => e.grantedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 配置外键关系
                entity.HasOne(e => e.role)
                      .WithMany(r => r.rolePermissions)
                      .HasForeignKey(e => e.roleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.permission)
                      .WithMany(p => p.rolePermissions)
                      .HasForeignKey(e => e.permissionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 配置 WechatAccount
            modelBuilder.Entity<WechatAccount>(entity =>
            {
                entity.ToTable("wechat_accounts");
                entity.HasKey(e => e.wxid);
                entity.HasIndex(e => e.wxid).IsUnique();
                entity.HasIndex(e => e.accountStatus);
                entity.HasIndex(e => e.isDeleted);
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 AppVersion
            modelBuilder.Entity<AppVersion>(entity =>
            {
                entity.ToTable("app_versions");
                entity.HasKey(e => e.versionId);
                entity.HasIndex(e => new { e.platform, e.versionNumber }).IsUnique();
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 SrClient
            modelBuilder.Entity<SrClient>(entity =>
            {
                entity.ToTable("sr_clients");
                entity.HasKey(e => e.uuid);
                
                // New Proto-JSONB Mapping
                entity.Property(e => e.device)
                      .HasColumnType("jsonb")
                      .HasConversion(
                          v => v == null ? "{}" : Google.Protobuf.JsonFormatter.Default.Format(v),
                          v => string.IsNullOrEmpty(v) ? new Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage() : new Google.Protobuf.JsonParser(Google.Protobuf.JsonParser.Settings.Default.WithIgnoreUnknownFields(true)).Parse<Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage>(v)
                      );
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Configure relationship with WechatAccount
                // Removed legacy 'accounts' relationship configuration as the property has been removed from SrClient.
                // The relationship is now managed primarily through WechatAccount.clientUuid foreign key.
            });

            // 配置视频号结果历史
            modelBuilder.Entity<FinderResultHistory>(entity =>
            {
                entity.ToTable("FinderResultHistory");
                entity.HasKey(e => e.id);
                entity.HasIndex(e => new { e.ownerKey, e.resultType, e.receivedAt });
                entity.HasIndex(e => new { e.ownerKey, e.resultType, e.taskId });
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.receivedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置手机短信记录。
            // 短信来自手机系统库，SmsId 只在同一设备内稳定，因此查询和幂等索引必须同时包含 ownerWxid 与 IMEI。
            modelBuilder.Entity<SmsRecord>(entity =>
            {
                entity.ToTable("SmsRecords");
                entity.HasKey(e => e.id);
                entity.HasIndex(e => new { e.ownerWxid, e.imei, e.smsId });
                entity.HasIndex(e => new { e.ownerWxid, e.imei, e.threadId, e.rawDate });
                entity.HasIndex(e => e.smsTime);
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置手机通话记录。
            // CallLogId 只在同一设备内稳定；录音 URL 由 Android 上传后写入 RecordUrl。
            modelBuilder.Entity<CallLogRecord>(entity =>
            {
                entity.ToTable("CallLogRecords");
                entity.HasKey(e => e.id);
                entity.HasIndex(e => new { e.ownerWxid, e.imei, e.callLogId });
                entity.HasIndex(e => new { e.ownerWxid, e.imei, e.rawDate, e.number, e.type });
                entity.HasIndex(e => e.callTime);
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 62203 群邀请审批索引。
            // MsgId 是审批主链使用的幂等键；ChatRoomId/UpdateTime 用于列表补偿兜底匹配；InvitationStatus 用于待审批筛选。
            modelBuilder.Entity<GroupInvitation>(entity =>
            {
                entity.HasIndex(e => new { e.weChatId, e.msgId });
                entity.HasIndex(e => new { e.weChatId, e.chatRoomId, e.updateTime });
                entity.HasIndex(e => new { e.weChatId, e.invitationStatus });
                entity.Property(e => e.invitedJson).HasColumnType("jsonb").HasDefaultValue("[]");
            });
        }
    }
}
