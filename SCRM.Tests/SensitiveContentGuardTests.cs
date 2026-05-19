using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.API.Models.Entities;
using SCRM.API.Services;
using SCRM.API.Services.Security;
using SCRM.Services.Data;

namespace SCRM.Tests;

/// <summary>
/// 服务端发送前敏感内容风控防回归测试。
/// <para>后台下发任务必须在服务端边界完成敏感词阻断，并且审计不能记录发送原文。</para>
/// </summary>
public sealed class SensitiveContentGuardTests
{
    [Fact]
    public void NormalizeWords_ShouldSupportCommonSeparatorsAndDeduplicate()
    {
        var words = SensitiveWordPolicyService.NormalizeWords(new[]
        {
            "测试A, 测试B\n测试C|测试A；测试D，测试E"
        });

        Assert.Equal(new[] { "测试A", "测试B", "测试C", "测试D", "测试E" }, words);
    }

    [Fact]
    public async Task CheckAsync_ShouldBlockAndAuditWithoutRawContent()
    {
        await using var db = CreateDbContext();
        db.SystemConfigs.Add(new SystemConfig
        {
            key = SensitiveWordPolicyService.BlockConfigKey,
            value = "测试禁词\n高危联系方式",
            description = "xunit",
            updatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sink = new CapturingSystemLogService();
        var guard = CreateGuard(db, sink);
        var result = await guard.CheckAsync(
            CreateUser("user-a"),
            "SendMessage",
            "device-a",
            "wx-a",
            "conversation",
            new Dictionary<string, string>
            {
                ["content"] = "请联系张三并使用测试禁词完成沟通"
            },
            "xunit");

        Assert.False(result.Allowed);
        Assert.Equal(SensitiveContentGuard.LevelBlock, result.Level);
        Assert.Contains("敏感词", result.Message);
        Assert.Contains("测试禁词", result.MatchedWords);

        Assert.Single(sink.Items);
        var item = sink.Items[0];
        Assert.Equal("Warning", item.Level);
        Assert.Equal(SensitiveContentGuard.ModuleName, item.Module);
        Assert.Equal(SensitiveContentGuard.ActionContentBlocked, item.Action);
        Assert.Equal("user-a", item.OperatorId);
        Assert.Equal("wx:wx-a", item.TargetId);
        Assert.DoesNotContain("请联系张三", item.Message);
        Assert.DoesNotContain("完成沟通", item.Message);

        using var doc = JsonDocument.Parse(item.Message);
        Assert.Equal("send-content", doc.RootElement.GetProperty("scope").GetString());
        Assert.Equal("SendMessage", doc.RootElement.GetProperty("scene").GetString());
        Assert.Equal("block", doc.RootElement.GetProperty("riskLevel").GetString());
        Assert.Contains(doc.RootElement.GetProperty("matchedWords").EnumerateArray(), word => word.GetString() == "测试禁词");
        Assert.Contains(doc.RootElement.GetProperty("fields").EnumerateArray(), field => field.GetString() == "content");
        Assert.True(doc.RootElement.GetProperty("contentLengths").GetProperty("content").GetInt32() > 0);
    }

    [Fact]
    public async Task CheckAsync_ShouldWarnButAllowWhenOnlyWarnPolicyMatches()
    {
        await using var db = CreateDbContext();
        db.SystemConfigs.Add(new SystemConfig
        {
            key = SensitiveWordPolicyService.WarnConfigKey,
            value = "提醒词",
            description = "xunit",
            updatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sink = new CapturingSystemLogService();
        var guard = CreateGuard(db, sink);
        var result = await guard.CheckAsync(
            CreateUser("user-a"),
            "PostMoment",
            "device-a",
            "wx-a",
            "moment",
            new Dictionary<string, string> { ["content"] = "朋友圈提醒词内容" },
            "xunit");

        Assert.True(result.Allowed);
        Assert.Equal(SensitiveContentGuard.LevelWarn, result.Level);
        Assert.Single(sink.Items);
        Assert.Equal(SensitiveContentGuard.ActionContentWarned, sink.Items[0].Action);
    }

    [Fact]
    public async Task CheckAsync_ShouldBlockNormalizedVariants()
    {
        await using var db = CreateDbContext();
        db.SystemConfigs.Add(new SystemConfig
        {
            key = SensitiveWordPolicyService.BlockConfigKey,
            value = "测试禁词\nriskword\n联系方式",
            description = "xunit",
            updatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sink = new CapturingSystemLogService();
        var guard = CreateGuard(db, sink);

        var result = await guard.CheckAsync(
            CreateUser("user-a"),
            "SendMessage",
            "device-a",
            "wx-a",
            "conversation",
            new Dictionary<string, string>
            {
                ["content"] = "测\u200B试　禁-词，请看 ＲＩＳＫ－ｗｏｒｄ，%E8%81%94%E7%B3%BB&#x65B9;&#x5F0F;"
            },
            "xunit");

        Assert.False(result.Allowed);
        Assert.Equal(SensitiveContentGuard.LevelBlock, result.Level);
        Assert.Contains("测试禁词", result.MatchedWords);
        Assert.Contains("riskword", result.MatchedWords);
        Assert.Contains("联系方式", result.MatchedWords);
        Assert.Single(sink.Items);
        Assert.DoesNotContain("ＲＩＳＫ", sink.Items[0].Message);
        Assert.DoesNotContain("测\u200B试", sink.Items[0].Message);
    }

    [Fact]
    public void NormalizeForRiskMatch_ShouldFoldWidthCaseAndSeparators()
    {
        var normalized = SensitiveContentGuard.NormalizeForRiskMatch(" Ａ-Ｂ＿Ｃ\u200B％２０ ");
        Assert.Equal("abc20", normalized);
    }

    [Fact]
    public async Task SaveDeviceBlockWordsAsync_ShouldPersistDeviceAndAccountPolicy()
    {
        await using var db = CreateDbContext();
        var policy = new SensitiveWordPolicyService(db, NullLogger<SensitiveWordPolicyService>.Instance);

        await policy.SaveDeviceBlockWordsAsync("device-a", "wx-a", new[] { "设备词", "账号词" });

        var deviceConfig = await db.SystemConfigs.SingleAsync(item => item.key == SensitiveWordPolicyService.BuildDeviceBlockConfigKey("device-a"));
        var accountConfig = await db.SystemConfigs.SingleAsync(item => item.key == SensitiveWordPolicyService.BuildAccountBlockConfigKey("wx-a"));
        Assert.Contains("设备词", deviceConfig.value);
        Assert.Contains("账号词", accountConfig.value);

        var loaded = await policy.GetPolicyAsync("device-a", "wx-a");
        Assert.Contains("设备词", loaded.BlockWords);
        Assert.Contains("账号词", loaded.BlockWords);
    }

    [Fact]
    public void SensitiveContentGuard_ShouldBeRegisteredAndUsedBySendEntrypoints()
    {
        var guard = ReadSource("SCRM.API", "Services", "Security", "SensitiveContentGuard.cs");
        var policy = ReadSource("SCRM.API", "Services", "Security", "SensitiveWordPolicyService.cs");
        var program = ReadSource("SCRM.API", "Program.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var query = ReadSource("SCRM.API", "Services", "Security", "SensitiveMediaAccessAuditQueryService.cs");
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "SensitiveMediaAccessAuditDtos.cs");
        var page = ReadSource("SCRM.UI", "Components", "Pages", "SensitiveMediaAccessAudit.razor");
        var deviceConfigPage = ReadSource("SCRM.UI", "Components", "Pages", "DeviceConfig.razor");

        Assert.Contains("public const string ModuleName = \"SensitiveContentRisk\"", guard);
        Assert.Contains("ActionContentBlocked", guard);
        Assert.Contains("不记录发送原文", guard);
        Assert.Contains("contentLengths", guard);
        Assert.Contains("BuildAuditTargetId", guard);

        Assert.Contains("BlockConfigKey", policy);
        Assert.Contains("DeviceBlockConfigPrefix", policy);
        Assert.Contains("AccountBlockConfigPrefix", policy);
        Assert.Contains("SaveDeviceBlockWordsAsync", policy);
        Assert.Contains("system_configs", policy, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("AddScoped<SCRM.API.Services.Security.SensitiveWordPolicyService>()", program);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.SensitiveContentGuard>()", program);

        Assert.Contains("SensitiveContentGuard sensitiveContentGuard", crm);
        Assert.Contains("SensitiveWordPolicyService sensitiveWordPolicyService", crm);
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", crm);
        Assert.Contains("SaveDeviceBlockWordsAsync(normalizedDeviceUuid, weChatId, normalizedWords)", crm);
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "SendMessageAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "SendGroupMessageAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "SendSmsAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "ForwardMessageByContentAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "PostMomentAdvancedAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "SphCommentAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "AddFriendWithSceneAsync"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "SendFriendVerifyAsync"));

        Assert.Contains("SensitiveContentGuard sensitiveContentGuard", hub);
        Assert.Contains("SensitiveWordPolicyService sensitiveWordPolicyService", hub);
        Assert.Contains("Context.GetHttpContext()", hub);
        Assert.Contains("SaveDeviceBlockWordsAsync(deviceUuid, weChatId, normalizedWords)", hub);
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "SendMessage"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "SendGroupMessage"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "SendSms"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "ForwardMessageByContent"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "PostMomentAdvanced"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "SphComment"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "AddFriendWithScene"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "SendFriendVerify"));

        Assert.Contains("SensitiveContentGuard.ModuleName", query);
        Assert.Contains("ActionContentBlocked", query);
        Assert.Contains("riskLevel", dto);
        Assert.Contains("matchedWords", dto);
        Assert.Contains("SensitiveContentRisk", page);
        Assert.Contains("ContentBlocked", page);
        Assert.Contains("服务端发送前阻断策略", deviceConfigPage);
    }

    [Fact]
    public void ForwardMessageEntrypoints_ShouldCheckOriginalMessageContent()
    {
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");

        foreach (var source in new[] { crm, hub })
        {
            Assert.Contains("BuildForwardMessageContentFieldsAsync", source);
            Assert.Contains("originalContent", source);
            Assert.Contains("originalContentXml", source);
            Assert.Contains("message.msgSvrId.HasValue", source);
            Assert.Contains("ids.Contains(message.msgSvrId.Value)", source);
        }

        foreach (var methodName in new[] { "ForwardMessageAsync", "ForwardMultiMessageAsync", "ForwardMessageByContentAsync" })
        {
            var method = ExtractMethod(crm, methodName);
            Assert.Contains("BuildForwardMessageContentFieldsAsync", method);
            Assert.Contains("riskFields", method);
            Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", method);
        }

        foreach (var methodName in new[] { "ForwardMessage", "ForwardMultiMessage", "ForwardMessageByContent" })
        {
            var method = ExtractMethod(hub, methodName);
            Assert.Contains("BuildForwardMessageContentFieldsAsync", method);
            Assert.Contains("riskFields", method);
            Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", method);
        }
    }

    private static SensitiveContentGuard CreateGuard(ApplicationDbContext db, CapturingSystemLogService sink)
    {
        var policy = new SensitiveWordPolicyService(db, NullLogger<SensitiveWordPolicyService>.Instance);
        return new SensitiveContentGuard(policy, sink, NullLogger<SensitiveContentGuard>.Instance);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId)
        }, "test"));
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf($" {methodName}(", StringComparison.Ordinal);
        if (methodNameIndex < 0)
        {
            methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        }
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}') depth--;
            if (depth == 0)
            {
                return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException($"方法体未闭合：{methodName}");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private sealed class CapturingSystemLogService : ISystemLogService
    {
        public List<(string Level, string Module, string Action, string Message, string? OperatorId, string? TargetId, string? ClientIp)> Items { get; } = new();

        public Task LogAsync(string level, string module, string action, string message, string? operatorId = null, string? targetId = null, string? clientIp = null)
        {
            Items.Add((level, module, action, message, operatorId, targetId, clientIp));
            return Task.CompletedTask;
        }

        public Task LogInfoAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Info", module, action, message, operatorId, targetId);
        }

        public Task LogWarningAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Warning", module, action, message, operatorId, targetId);
        }

        public Task LogErrorAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Error", module, action, message, operatorId, targetId);
        }
    }
}
