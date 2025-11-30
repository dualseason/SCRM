using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Models.Identity;

namespace SCRM.Services.Data
{
    /// <summary>
    /// 扩展的ApplicationDbContext配置，包括所有新增表的DbSet属性
    /// 这是一个模板文件，展示如何为所有表添加DbSet属性
    /// </summary>
    public partial class ApplicationDbContextExtended : DbContext
    {
        public ApplicationDbContextExtended(DbContextOptions<ApplicationDbContextExtended> options) : base(options)
        {
        }

        // ==================== 既有实体集合 ====================
        public DbSet<SCRM.Models.Identity.User> IdentityUsers { get; set; }
        public DbSet<SCRM.API.Models.Entities.Role> Roles { get; set; }
        public DbSet<SCRM.API.Models.Entities.Permission> Permissions { get; set; }
        public DbSet<SCRM.API.Models.Entities.UserRole> UserRoles { get; set; }
        public DbSet<SCRM.API.Models.Entities.RolePermission> RolePermissions { get; set; }

        // ==================== 一、设备与账号管理 ====================
        public DbSet<Device> Devices { get; set; }
        public DbSet<WechatAccount> WechatAccounts { get; set; }
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置所有Entity的表映射和关系
            // 这里可以添加更详细的Fluent API配置

            // 示例：配置WechatAccount
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

            // 示例：配置Device
            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("devices");
                entity.HasKey(e => e.DeviceId);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 注意：需要为其他所有Entity添加类似的配置
            // 可以使用反射自动化这个过程
        }
    }
}
