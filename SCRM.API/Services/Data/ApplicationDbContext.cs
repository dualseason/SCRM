using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Models.Identity;

namespace SCRM.Services.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // 身份验证和授权实体集合
        public DbSet<SCRM.Models.Identity.User> IdentityUsers { get; set; }
        public DbSet<SCRM.API.Models.Entities.Role> Roles { get; set; }
        public DbSet<SCRM.API.Models.Entities.Permission> Permissions { get; set; }
        public DbSet<SCRM.API.Models.Entities.UserRole> UserRoles { get; set; }
        public DbSet<SCRM.API.Models.Entities.RolePermission> RolePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置身份验证相关实体
            // 配置 IdentityUser 实体
            modelBuilder.Entity<Models.Identity.User>(entity =>
            {
                entity.ToTable("IdentityUsers");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserName).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Role 实体
            modelBuilder.Entity<SCRM.API.Models.Entities.Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.RoleId);
                entity.HasIndex(e => e.RoleName).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 Permission 实体
            modelBuilder.Entity<SCRM.API.Models.Entities.Permission>(entity =>
            {
                entity.ToTable("permissions");
                entity.HasKey(e => e.PermissionId);
                entity.HasIndex(e => e.PermissionCode).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 配置 UserRole 关系
            modelBuilder.Entity<SCRM.API.Models.Entities.UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(e => e.UserRoleId);
                entity.HasIndex(e => new { e.AccountId, e.RoleId }).IsUnique();
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
            modelBuilder.Entity<SCRM.API.Models.Entities.RolePermission>(entity =>
            {
                entity.ToTable("role_permissions");
                entity.HasKey(e => e.RolePermId);
                entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
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
        }
    }
}