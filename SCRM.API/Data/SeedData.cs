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
            var nettyHost = config["NettySettings:Host"] ?? "127.0.0.1";
            var nettyPort = config["NettySettings:Port"] ?? "8647";
            var nettyHttpPort = config["NettySettings:HttpPort"] ?? "42718";
            
            // Smart Default: Use configured HTTP port, not 5000
            var apiBaseUrl = config["ApiSettings:BaseUrl"] ?? $"http://{nettyHost}:{nettyHttpPort}";

            // Critical Settings that MUST match appsettings.json if changed
            var criticalKeys = new HashSet<string> 
            { 
                "tcpServerHost", "tcpServerPort", "httpApiBaseUrl", "fileUploadUrl", "autoUpdateUrl" 
            };

            var defaults = new Dictionary<string, (string value, string desc)>
            {
                { "tcpServerHost", (nettyHost, "TCP服务器地址 (Synced with AppSettings)") },
                { "tcpServerPort", (nettyPort, "TCP服务器端口 (Synced with AppSettings)") },
                { "httpApiBaseUrl", (apiBaseUrl, "API基础URL (Synced with AppSettings)") },
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
                else
                {
                    // For Critical Keys, force update if value changed in AppSettings
                    if (criticalKeys.Contains(key) && existing.value != val)
                    {
                        existing.value = val;
                        // existing.description = desc; // Optional: update description too
                        existing.updatedAt = DateTime.UtcNow;
                        changesMade = true;
                    }
                }
            }

            if (changesMade)
            {
                await db.SaveChangesAsync();
            }
        }
    }
}
