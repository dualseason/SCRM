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
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        // ==================== 一、设备与账号管理 ====================
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceAuthorization> DeviceAuthorizations { get; set; }
        public DbSet<DeviceHeartbeat> DeviceHeartbeats { get; set; }
        public DbSet<DeviceVersionLog> DeviceVersionLogs { get; set; }
        public DbSet<AccountStatusLog> AccountStatusLogs { get; set; }
        public DbSet<DeviceCommand> DeviceCommands { get; set; }
        public DbSet<DeviceStatusLog> DeviceStatusLogs { get; set; }
        public DbSet<DeviceLocation> DeviceLocations { get; set; }

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
        public DbSet<SystemNotification> SystemNotifications { get; set; }
        public DbSet<AppVersion> AppVersions { get; set; }
        public DbSet<SrClient> SrClients { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 LegacyWechatUser 实体
            modelBuilder.Entity<LegacyWechatUser>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.UserName).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Role 实体
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.RoleId);
                entity.HasIndex(e => e.RoleName);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Permission 实体
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("permissions");
                entity.HasKey(e => e.PermissionId);
                entity.HasIndex(e => e.PermissionCode);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 UserRole 关系
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(e => e.UserRoleId);
                entity.HasIndex(e => new { e.AccountId, e.RoleId });
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 配置外键关系
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Role)
                      .WithMany(r => r.UserRoles)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedByAccount)
                      .WithMany()
                      .HasForeignKey(e => e.AssignedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // 配置 RolePermission 关系
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("role_permissions");
                entity.HasKey(e => e.RolePermId);
                entity.HasIndex(e => new { e.RoleId, e.PermissionId });
                entity.Property(e => e.GrantedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 配置外键关系
                entity.HasOne(e => e.Role)
                      .WithMany(r => r.RolePermissions)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Permission)
                      .WithMany(p => p.RolePermissions)
                      .HasForeignKey(e => e.PermissionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 配置 WechatAccount
            modelBuilder.Entity<WechatAccount>(entity =>
            {
                entity.ToTable("wechat_accounts");
                entity.HasKey(e => e.AccountId);
                entity.HasIndex(e => e.Wxid).IsUnique();
                entity.HasIndex(e => e.AccountStatus);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Device
            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("devices");
                entity.HasKey(e => e.DeviceId);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 AppVersion
            modelBuilder.Entity<AppVersion>(entity =>
            {
                entity.ToTable("app_versions");
                entity.HasKey(e => e.VersionId);
                entity.HasIndex(e => new { e.Platform, e.VersionNumber }).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 SrClient
            modelBuilder.Entity<SrClient>(entity =>
            {
                entity.ToTable("sr_clients");
                entity.HasKey(e => e.uuid);
                entity.OwnsOne(e => e.device, b => b.ToJson());
                entity.Property(e => e.createdAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.updatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Configure relationship with WechatAccount
                entity.HasMany(e => e.Accounts)
                      .WithOne()
                      .HasForeignKey(w => w.ClientUuid)
                      .HasPrincipalKey(e => e.uuid);
            });
        }
    }
}