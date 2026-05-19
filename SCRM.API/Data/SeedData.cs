using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCRM.SHARED.Models;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using SCRM.API.Services.Data;
using SCRM.Models.Constants;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.API.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(SeedData));

            // Ensure Admin Role exists
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // Seed Admin User
            var adminEmail = "skradmin@qq.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "123456"); // Default password
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            
            // Seed System Config from appsettings.json
            await EnsureSystemConfigAsync(dbContext, config);

            // Seed security/audit permissions used by sensitive media audit page.
            await EnsureSecurityAuditPermissionsAsync(dbContext);

            // [新增调用] 服务端启动时清理“假在线”设备缓存并更新数据库状态
            try 
            {
                var (clientCount, wechatCount) = await dbContext.ResetAllDevicesToOffline();
                logger?.LogInformation("[System Initialized] 启动重置完成，已重置上线设备数: {ClientCount}，重置在线微信数: {WechatCount}", clientCount, wechatCount);
            }
            catch(Exception ex)
            {
                logger?.LogError(ex, "[System Initialization Error] 启动时重置设备离线状态失败");
            }
        }

        public static async Task EnsureSecurityAuditPermissionsAsync(ApplicationDbContext db)
        {
            var now = DateTime.UtcNow;
            var defaults = new[]
            {
                new Permission
                {
                    permissionName = "查看敏感媒体访问审计",
                    permissionCode = Permissions.Risk.AuditView,
                    permissionType = 3,
                    description = "允许查看短期媒体 token 签发、拒绝、打开和打开失败审计记录",
                    isSensitive = true,
                    isSystem = true,
                    isDeleted = false,
                    createdAt = now,
                    updatedAt = now
                },
                BuildSensitivePermission("设备同步任务", Permissions.DeviceTask.Sync, "允许下发通讯录、群、消息、朋友圈等同步类设备任务", now),
                BuildSensitivePermission("设备配置管理", Permissions.DeviceTask.ConfigManage, "允许下发普通设备配置与配置刷新任务", now),
                BuildSensitivePermission("设备敏感配置管理", Permissions.DeviceTask.SensitiveConfigManage, "允许下发微信违禁词、敏感开关等会影响风控策略的配置", now),
                BuildSensitivePermission("设备截图", Permissions.DeviceTask.Screenshot, "允许请求 Android 设备截图并进入受控媒体访问链路", now),
                BuildSensitivePermission("删除设备", Permissions.DeviceTask.DeleteDevice, "允许删除 SCRM 设备记录并通知 Android 断开", now),
                BuildSensitivePermission("微信定位查询", Permissions.WechatOperation.LocationQuery, "允许查询微信当前位置或定位快照", now),
                BuildSensitivePermission("微信零钱查询", Permissions.WechatOperation.WalletQuery, "允许查询微信零钱余额", now),
                BuildSensitivePermission("微信账号登出", Permissions.WechatOperation.Logout, "允许下发微信登出高危任务", now),
                BuildSensitivePermission("微信设置修改", Permissions.WechatOperation.SettingUpdate, "允许修改微信设置、隐私和相关开关", now),
                BuildSensitivePermission("微信二维码拉取", Permissions.WechatOperation.QrCodePull, "允许拉取个人或群二维码", now),
                BuildSensitivePermission("A8Key 查询", Permissions.WechatOperation.A8KeyQuery, "允许请求微信 A8Key 链接解析", now),
                BuildSensitivePermission("撤回微信消息", Permissions.MessageOperation.Revoke, "允许下发微信消息撤回任务", now),
                BuildSensitivePermission("清空微信端聊天记录", Permissions.MessageOperation.ClearWechat, "允许清空微信端聊天记录", now),
                BuildSensitivePermission("拉取原始聊天内容", Permissions.MessageOperation.PullOriginal, "允许请求原始消息内容、详情或下载链路", now),
                BuildSensitivePermission("语音转文字", Permissions.MessageOperation.VoiceTransText, "允许请求语音消息转文字", now),
                BuildSensitivePermission("删除微信好友", Permissions.ContactOperation.DeleteWechat, "允许从微信端删除好友", now),
                BuildSensitivePermission("设置好友权限", Permissions.ContactOperation.PermissionSet, "允许设置仅聊天、朋友圈可见等好友权限", now),
                BuildSensitivePermission("联系人标签管理", Permissions.ContactOperation.LabelManage, "允许新建、删除和修改微信联系人标签", now),
                BuildSensitivePermission("群管理", Permissions.GroupOperation.Manage, "允许修改群名、群公告、群备注、群权限等群管理动作", now),
                BuildSensitivePermission("群成员添加", Permissions.GroupOperation.MemberAdd, "允许向群聊添加成员", now),
                BuildSensitivePermission("群成员移除", Permissions.GroupOperation.MemberKick, "允许从群聊移除成员", now),
                BuildSensitivePermission("二维码入群", Permissions.GroupOperation.JoinByQr, "允许通过群二维码入群", now),
                BuildSensitivePermission("群邀请审批", Permissions.GroupOperation.InviteApprove, "允许同意或处理群邀请", now),
                BuildSensitivePermission("退出群聊", Permissions.GroupOperation.Exit, "允许下发退出群聊任务", now),
                BuildSensitivePermission("发布朋友圈", Permissions.MomentOperation.Post, "允许发布朋友圈正文、图片、链接、视频、可见范围、位置和提醒谁看等内容", now),
                BuildSensitivePermission("删除朋友圈", Permissions.MomentOperation.Delete, "允许删除朋友圈内容或评论", now),
                BuildSensitivePermission("朋友圈互动", Permissions.MomentOperation.Interact, "允许点赞、评论或处理朋友圈互动消息", now),
                BuildSensitivePermission("视频号读取", Permissions.FinderOperation.Read, "允许读取视频号通知、评论或用户页", now),
                BuildSensitivePermission("视频号互动", Permissions.FinderOperation.Interact, "允许视频号点赞、评论、发布等互动动作", now),
                BuildSensitivePermission("视频号删除评论", Permissions.FinderOperation.DeleteComment, "允许删除视频号评论", now),
                BuildSensitivePermission("视频号导出", Permissions.FinderOperation.Export, "允许导出视频号历史；字段仍按脱敏权限控制", now),
                BuildSensitivePermission("视频号原始字段查看", Permissions.FinderOperation.ViewRaw, "允许查看视频号 NonceId 等原始操作上下文字段", now),
                BuildSensitivePermission("视频号媒体字段查看", Permissions.FinderOperation.ViewMedia, "允许查看视频号头像、封面和媒体地址", now),
                BuildSensitivePermission("视频号指标查看", Permissions.FinderOperation.ViewMetrics, "允许查看视频号阅读、点赞、评论、收藏和转发指标", now)
            };

            var changed = false;
            foreach (var permission in defaults)
            {
                var existing = await db.permissions.FirstOrDefaultAsync(item => item.permissionCode == permission.permissionCode);
                if (existing == null)
                {
                    await db.permissions.AddAsync(permission);
                    changed = true;
                    continue;
                }

                if (existing.isDeleted
                    || existing.permissionName != permission.permissionName
                    || existing.description != permission.description
                    || !existing.isSystem
                    || !existing.isSensitive
                    || existing.permissionType != permission.permissionType)
                {
                    existing.permissionName = permission.permissionName;
                    existing.description = permission.description;
                    existing.permissionType = permission.permissionType;
                    existing.isSensitive = true;
                    existing.isSystem = true;
                    existing.isDeleted = false;
                    existing.updatedAt = now;
                    changed = true;
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync();
            }
        }

        private static Permission BuildSensitivePermission(string name, string code, string description, DateTime now)
        {
            return new Permission
            {
                permissionName = name,
                permissionCode = code,
                permissionType = 3,
                description = description,
                isSensitive = true,
                isSystem = true,
                isDeleted = false,
                createdAt = now,
                updatedAt = now
            };
        }

        public static async Task EnsureSystemConfigAsync(ApplicationDbContext db, IConfiguration config)
        {
            // 移除 Netty 配置对 AppSettings 的强依赖。
            // 数据库现在是唯一的配置来源。
            
            var apiBaseUrl = config["ApiSettings:BaseUrl"] ?? "http://192.168.2.226:42718";

            var defaults = new Dictionary<string, (string value, string desc)>
            {
                { "server_port", ("42719", "TCP监听端口 (主数据源: DB)") },
                { "tcpServerHost", ("192.168.2.226", "TCP服务器地址 (主数据源: DB. 客户端连接此处)") },
                { "httpApiBaseUrl", (apiBaseUrl, "API基础URL") },
                { "autoLogin", ("false", "自动登录") },
                { "autoPic", ("true", "自动下载图片") },
                { "silentFunc", ("false", "静默功能") },
                { "forceRun", ("true", "强制运行") },
                { "keepWake", ("5", "保持唤醒间隔(分钟)") },
                { "logLevel", ("INFO", "日志级别") },
                { "clientConfigPath", ("/sdcard/Android/media/.cache/sys_config.dat", "客户端配置路径") },
                { "fileUploadUrl", ($"{apiBaseUrl}/fileUpload", "文件上传URL") },
                { "autoUpdateUrl", ($"{apiBaseUrl}/download/app-release.apk", "自动更新URL") }
            };

            var changesMade = false;
            foreach (var kvp in defaults)
            {
                var key = kvp.Key;
                var (val, desc) = kvp.Value;

                var existing = await db.SystemConfigs.FirstOrDefaultAsync(c => c.key == key);
                
                if (existing == null)
                {
                    await db.SystemConfigs.AddAsync(new SystemConfig 
                    { 
                        key = key, 
                        value = val, 
                        description = desc 
                    });
                    changesMade = true;
                }
                // 避免从代码或 AppSettings 覆盖数据库中的运行期配置。
                // 我们尊重数据库中的现有值。
            }

            if (changesMade)
            {
                await db.SaveChangesAsync();
            }
        }
    }
}
