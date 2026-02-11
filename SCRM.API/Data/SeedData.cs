using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SCRM.SHARED.Models;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
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
        }

        public static async Task EnsureSystemConfigAsync(ApplicationDbContext db, IConfiguration config)
        {
            // [AntiGravity] 重构: 移除了 Netty 配置对 AppSettings 的依赖。
            // 数据库现在是唯一的配置来源。
            
            var apiBaseUrl = config["ApiSettings:BaseUrl"] ?? "http://192.168.2.226:42718";

            var defaults = new Dictionary<string, (string value, string desc)>
            {
                { "server_port", ("8647", "TCP监听端口 (主数据源: DB)") },
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
                // [AntiGravity] 移除了从代码/AppSettings 覆盖数据库值的逻辑。
                // 我们尊重数据库中的现有值。
            }

            if (changesMade)
            {
                await db.SaveChangesAsync();
            }
        }
    }
}
