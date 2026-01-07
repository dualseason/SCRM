using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
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
                entity.HasKey(e => e.accountId);
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
                entity.HasMany(e => e.accounts)
                      .WithOne(w => w.Client)
                      .HasForeignKey(w => w.clientUuid)
                      .HasPrincipalKey(e => e.uuid);
            });
        }
    }
}