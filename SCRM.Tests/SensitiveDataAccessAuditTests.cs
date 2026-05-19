using System.Security.Claims;
using System.Text.Json;
using SCRM.API.Services;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.Tests;

/// <summary>
/// 手机号、短信正文、通话录音标记等敏感数据查看审计防回归测试。
/// <para>列表接口返回完整号码或短信正文时必须写 system_logs 审计，同时通话列表不能再把永久 recordUrl 发给浏览器。</para>
/// </summary>
public sealed class SensitiveDataAccessAuditTests
{
    [Fact]
    public void PhoneRecordDtosAndMasking_ShouldUseHasRecordingWithoutLeakingRecordUrl()
    {
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "PhoneRecordDtos.cs");
        var dbHelper = ReadSource("SCRM.API", "Services", "Data", "DbHelper.cs");
        var masking = ReadSource("SCRM.API", "Services", "Security", "SensitiveMaskingService.cs");
        var page = ReadSource("SCRM.UI", "Components", "Pages", "PhoneRecords.razor");

        Assert.Contains("public bool hasRecording { get; set; }", dto);
        Assert.Contains("hasRecording = item.recordUrl != null && item.recordUrl != string.Empty", dbHelper);
        Assert.Contains("hasRecording = profile.CanViewCallRecordUrl && (record.hasRecording || !string.IsNullOrWhiteSpace(record.recordUrl))", masking);
        Assert.Contains("recordUrl = string.Empty", masking);
        Assert.Contains("!item.hasRecording", page);
        Assert.Contains("CreateCallRecordingAccessTokenAsync(item.id, 5)", page);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(item.recordUrl)", page);
        Assert.DoesNotContain("href=\"@item.recordUrl\"", page);
        Assert.DoesNotContain("src=\"@item.recordUrl\"", page);
    }

    [Fact]
    public void SensitiveDataAccessAudit_ShouldBeRegisteredAndUsedByPhoneRecordReaders()
    {
        var service = ReadSource("SCRM.API", "Services", "Security", "SensitiveDataAccessAuditService.cs");
        var program = ReadSource("SCRM.API", "Program.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var query = ReadSource("SCRM.API", "Services", "Security", "SensitiveMediaAccessAuditQueryService.cs");
        var page = ReadSource("SCRM.UI", "Components", "Pages", "SensitiveMediaAccessAudit.razor");

        Assert.Contains("public const string ModuleName = \"SensitiveDataAccess\"", service);
        Assert.Contains("ActionSensitiveFieldsReturned", service);
        Assert.Contains("ISystemLogService systemLogService", service);
        Assert.Contains("_systemLogService.LogAsync(", service);
        Assert.Contains("fields", service);
        Assert.Contains("recordCount", service);
        Assert.Contains("不记录手机号、短信正文、录音 URL 等原文", service);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.SensitiveDataAccessAuditService>()", program);

        Assert.Contains("SensitiveDataAccessAuditService sensitiveDataAccessAuditService", crm);
        Assert.Contains("_sensitiveDataAccessAuditService = sensitiveDataAccessAuditService;", crm);
        Assert.Contains("LogSensitiveFieldsReturnedAsync", ExtractMethod(crm, "GetSmsRecordsAsync"));
        Assert.Contains("BuildSmsSensitiveFields(records, profile)", ExtractMethod(crm, "GetSmsRecordsAsync"));
        Assert.Contains("SensitiveMaskingService.MaskSmsRecord(record, profile)", ExtractMethod(crm, "GetSmsRecordsAsync"));
        Assert.Contains("BuildCallLogSensitiveFields(records, profile)", ExtractMethod(crm, "GetCallLogRecordsAsync"));
        Assert.Contains("SensitiveMaskingService.MaskCallLogRecord(record, profile)", ExtractMethod(crm, "GetCallLogRecordsAsync"));

        Assert.Contains("SensitiveDataAccessAuditService sensitiveDataAccessAuditService", hub);
        Assert.Contains("_sensitiveDataAccessAuditService = sensitiveDataAccessAuditService;", hub);
        Assert.Contains("Context.GetHttpContext()", hub);

        Assert.Contains("SensitiveDataAccessAuditService.ModuleName", query);
        Assert.Contains("GetString(root, \"dataKind\")", query);
        Assert.Contains("GetStringArray(root, \"fields\")", query);
        Assert.Contains("GetInt32(root, \"recordCount\")", query);
        Assert.Contains("SensitiveFieldsReturned", page);
        Assert.Contains("SensitiveDataAccess", page);
    }

    [Fact]
    public void MaskCallLogRecord_ShouldAlwaysHidePermanentRecordUrlAndKeepAvailabilityFlag()
    {
        var call = new CallLogRecordDto
        {
            number = "13812345678",
            recordUrl = "https://file.local/uploads/wx/wx-a/call/a.amr"
        };

        var fullProfile = SensitiveAccessProfile.FullAccess;
        var masked = SensitiveMaskingService.MaskCallLogRecord(call, fullProfile);

        Assert.True(masked.hasRecording);
        Assert.Equal(string.Empty, masked.recordUrl);
        Assert.Equal("13812345678", masked.number);

        var noAccess = SensitiveMaskingService.MaskCallLogRecord(call, SensitiveAccessProfile.NoAccess);
        Assert.False(noAccess.hasRecording);
        Assert.Equal(string.Empty, noAccess.recordUrl);
    }

    [Fact]
    public async Task SensitiveDataAccessAuditService_ShouldWriteSystemLogWithoutRawValues()
    {
        var sink = new CapturingSystemLogService();
        var service = new SensitiveDataAccessAuditService(
            sink,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SensitiveDataAccessAuditService>.Instance);

        await service.LogSensitiveFieldsReturnedAsync(
            CreateUser("user-a"),
            "SmsRecords",
            "wx-a",
            new[] { "number", "smsContent" },
            3,
            "GetSmsRecordsAsync",
            imei: "imei-a",
            detail: "count=3");

        Assert.Single(sink.Items);
        var item = sink.Items[0];
        Assert.Equal("Info", item.Level);
        Assert.Equal(SensitiveDataAccessAuditService.ModuleName, item.Module);
        Assert.Equal(SensitiveDataAccessAuditService.ActionSensitiveFieldsReturned, item.Action);
        Assert.Equal("user-a", item.OperatorId);
        Assert.Equal("wx:wx-a", item.TargetId);
        Assert.DoesNotContain("13812345678", item.Message);
        Assert.DoesNotContain("验证码", item.Message);
        Assert.DoesNotContain("record.amr", item.Message);

        using var doc = JsonDocument.Parse(item.Message);
        Assert.Equal("SmsRecords", doc.RootElement.GetProperty("dataKind").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("recordCount").GetInt32());
        Assert.Contains(doc.RootElement.GetProperty("fields").EnumerateArray(), field => field.GetString() == "number");
        Assert.Contains(doc.RootElement.GetProperty("fields").EnumerateArray(), field => field.GetString() == "smsContent");
    }

    private static ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId)
        }, "test"));
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

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

        var depth = 0;
        for (var index = braceStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(index + 1)];
                }
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
}

