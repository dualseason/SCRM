namespace SCRM.Tests;

/// <summary>
/// 敏感媒体访问审计防回归测试。
/// <para>聊天媒体、通话录音、截图的短期 token 签发和实际打开都必须写入 system_logs 审计，不新增单独持久化表。</para>
/// </summary>
public sealed class SensitiveMediaAccessAuditTests
{
    [Fact]
    public void AuditService_ShouldReuseSystemLogsAndAvoidTokenPlaintext()
    {
        var source = ReadSource("SCRM.API", "Services", "Security", "SensitiveMediaAccessAuditService.cs");
        var program = ReadSource("SCRM.API", "Program.cs");
        var db = ReadSource("SCRM.API", "Services", "Data", "DbHelper.cs");
        var systemLog = ReadSource("SCRM.SHARED", "Models", "Entities", "SystemLog.cs");

        Assert.Contains("public class SensitiveMediaAccessAuditService", source);
        Assert.Contains("public const string ModuleName = \"SensitiveMediaAccess\"", source);
        Assert.Contains("ActionTokenIssued", source);
        Assert.Contains("ActionTokenDenied", source);
        Assert.Contains("ActionTokenOpened", source);
        Assert.Contains("ActionTokenOpenDenied", source);
        Assert.Contains("ISystemLogService systemLogService", source);
        Assert.Contains("_systemLogService.LogAsync(", source);
        Assert.Contains("HashToken(token)", source);
        Assert.Contains("不记录短期 token 明文", source);
        Assert.Contains("relativePath", source);
        Assert.Contains("targetKind", source);
        Assert.Contains("targetValue", source);
        Assert.Contains("userAgent", source);
        Assert.Contains("queryKeys", source);

        Assert.Contains("builder.Services.AddScoped<SCRM.API.Services.Security.SensitiveMediaAccessAuditService>()", program);
        Assert.Contains("public static class DbHelper", db);
        Assert.Contains("[Table(\"system_logs\")]", systemLog);
    }

    [Fact]
    public void MediaAccessController_ShouldAuditTokenIssueAndOpen()
    {
        var source = ReadSource("SCRM.API", "Controllers", "MediaAccessController.cs");
        var createToken = ExtractMethod(source, "CreateToken(");
        var createCall = ExtractMethod(source, "CreateCallRecordingToken(");
        var open = ExtractMethod(source, "Open(");

        Assert.Contains("SensitiveMediaAccessAuditService auditService", source);
        Assert.Contains("_auditService = auditService;", source);

        Assert.Contains("LogTokenDeniedAsync", createToken);
        Assert.Contains("LogTokenIssuedAsync", createToken);
        Assert.Contains("nameof(CreateToken)", createToken);

        Assert.Contains("LogTokenDeniedAsync", createCall);
        Assert.Contains("LogTokenIssuedAsync", createCall);
        Assert.Contains("nameof(CreateCallRecordingToken)", createCall);
        Assert.Contains("CallLogId=", createCall);

        Assert.Contains("public async Task<IActionResult> Open", open);
        Assert.Contains("LogTokenOpenDeniedAsync", open);
        Assert.Contains("LogTokenOpenedAsync", open);
        Assert.Contains("nameof(Open)", open);
        Assert.Contains("TryUnprotect(token", open);
        Assert.Contains("TryResolvePhysicalPath(payload.RelativePath", open);
    }

    [Fact]
    public void CrmService_ShouldAuditDirectUiTokenIssueMethods()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var screenshot = ExtractMethod(source, "CreateScreenshotAccessTokenAsync");
        var recording = ExtractMethod(source, "CreateCallRecordingAccessTokenAsync");
        var media = ExtractMethod(source, "CreateMediaAccessTokenAsync");

        Assert.Contains("SensitiveMediaAccessAuditService sensitiveMediaAccessAuditService", source);
        Assert.Contains("_sensitiveMediaAccessAuditService = sensitiveMediaAccessAuditService;", source);

        foreach (var method in new[] { screenshot, recording, media })
        {
            Assert.Contains("LogTokenDeniedAsync", method);
            Assert.Contains("LogTokenIssuedAsync", method);
            Assert.Contains("MediaAccessTokenDto.Ok", method);
        }

        Assert.Contains("MediaAccessTokenService.ScreenshotScope", screenshot);
        Assert.Contains("MediaAccessTokenService.CallRecordingScope", recording);
        Assert.Contains("MediaAccessTokenService.MediaScope", media);
    }

    [Fact]
    public void NoDedicatedAuditMigrationOrEntity_ShouldBeIntroduced()
    {
        var root = FindSolutionRoot();
        var migrationFiles = Directory.GetFiles(Path.Combine(root, "SCRM.API", "Migrations"), "*Sensitive*Audit*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "SCRM.API", "Migrations"), "*Media*Audit*", SearchOption.TopDirectoryOnly))
            .ToArray();
        var entityFiles = Directory.GetFiles(Path.Combine(root, "SCRM.API", "Models", "Entities"), "*Audit*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "SCRM.SHARED", "Models", "Entities"), "*Audit*", SearchOption.TopDirectoryOnly))
            .ToArray();

        Assert.Empty(migrationFiles);
        Assert.Empty(entityFiles);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var searchFrom = 0;
        while (true)
        {
            var methodNameIndex = source.IndexOf(methodName, searchFrom, StringComparison.Ordinal);
            Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

            var start = Math.Max(
                source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal),
                source.LastIndexOf("private async Task", methodNameIndex, StringComparison.Ordinal));
            start = Math.Max(start, source.LastIndexOf("public Task", methodNameIndex, StringComparison.Ordinal));
            start = Math.Max(start, source.LastIndexOf("public async", methodNameIndex, StringComparison.Ordinal));
            Assert.True(start >= 0, $"未找到方法声明：{methodName}");

            var braceStart = source.IndexOf('{', start);
            Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

            var header = source[start..braceStart];
            if (!header.Contains(methodName, StringComparison.Ordinal))
            {
                searchFrom = methodNameIndex + methodName.Length;
                continue;
            }

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
