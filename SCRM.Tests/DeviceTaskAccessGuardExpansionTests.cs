using System.Text.RegularExpressions;

namespace SCRM.Tests;

/// <summary>
/// 设备任务归属校验扩面防回归测试。
/// <para>所有通过 deviceUuid 下发 Android 任务的 Hub/CrmService 入口，都必须在服务端边界先做设备归属校验。</para>
/// </summary>
public sealed class DeviceTaskAccessGuardExpansionTests
{
    [Fact]
    public void ClientHub_DeviceTaskMethods_ShouldUseUnifiedDeviceAccessGuard()
    {
        var source = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var methods = ExtractPublicAsyncMethods(source)
            .Where(method => method.Parameters.Contains("deviceUuid", StringComparison.Ordinal)
                && (method.ReturnType == "TaskResult" || method.ReturnType == "bool"))
            .ToList();

        Assert.NotEmpty(methods);
        Assert.DoesNotContain(methods, method => !HasDeviceGuard(method.Body));
        Assert.Contains("DenyIfNoDeviceAccessAsync", source);
        Assert.Contains("用于无文字内容的设备任务", source);
    }

    [Fact]
    public void CrmService_DeviceTaskMethods_ShouldUseUnifiedDeviceAccessGuard()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var methods = ExtractPublicAsyncMethods(source)
            .Where(method => method.Parameters.Contains("deviceUuid", StringComparison.Ordinal)
                && (method.ReturnType == "TaskResult" || method.ReturnType == "bool"))
            .ToList();

        Assert.NotEmpty(methods);
        Assert.DoesNotContain(methods, method => !HasDeviceGuard(method.Body));
        Assert.Contains("DenyIfNoDeviceAccessAsync", source);
        Assert.Contains("用于无文字内容的设备任务", source);
    }

    [Fact]
    public void AccountScopedHubAndCrmServiceMethods_ShouldCheckAccountOwnership()
    {
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        foreach (var methodName in new[]
        {
            "GetGroupInvitations",
            "GetContactLabels",
            "GetMassSendHistory",
            "GetAccountSettings",
            "UpdateAccountSettings"
        })
        {
            var method = ExtractMethod(hub, methodName);
            Assert.Contains($"CanAccessAccountOrLogAsync", method);
            Assert.Contains($"nameof({methodName})", method);
        }

        foreach (var methodName in new[]
        {
            "GetAccountSettingsAsync",
            "UpdateAccountSettingsAsync"
        })
        {
            var method = ExtractMethod(crm, methodName);
            Assert.Contains("_accountAccessGuard.CanAccessAccountAsync", method);
            Assert.Contains("GetCurrentUserAsync", method);
            Assert.Contains("拒绝越权账号", method);
        }
    }

    [Fact]
    public void StaticVerifier_ShouldCoverDeviceTaskExpansion()
    {
        var verifier = ReadSmRunSource("tools", "verify_scrm_device_task_access_guard.py");
        var progress = ReadSmRunSource("docs", "Codex_持续开发进度_2026-05-18.md");

        Assert.Contains("verify_scrm_device_task_access_guard.py", verifier);
        Assert.Contains("DenyIfNoDeviceAccessAsync", verifier);
        Assert.Contains("GetAccountSettings", verifier);
        Assert.Contains("v381", progress);
    }

    private static bool HasDeviceGuard(string method)
    {
        return method.Contains("CanAccessDeviceOrLogAsync", StringComparison.Ordinal)
            || method.Contains("CanAccessDeviceAsync", StringComparison.Ordinal)
            || method.Contains("DenyIfDeviceOrSensitiveContentAsync", StringComparison.Ordinal)
            || method.Contains("DenyIfNoDeviceAccessAsync", StringComparison.Ordinal)
            || method.Contains("DenyIfHighRiskDeviceOperationAsync", StringComparison.Ordinal);
    }

    private static List<AsyncMethod> ExtractPublicAsyncMethods(string source)
    {
        var result = new List<AsyncMethod>();
        var regex = new Regex(@"public\s+async\s+Task(?:<(?<returnType>[^>]+)>)?\s+(?<name>\w+)\s*\((?<params>[^)]*)\)", RegexOptions.Multiline);
        foreach (Match match in regex.Matches(source))
        {
            var braceStart = source.IndexOf('{', match.Index + match.Length);
            if (braceStart < 0)
            {
                continue;
            }

            var depth = 0;
            for (var i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}') depth--;
                if (depth == 0)
                {
                    result.Add(new AsyncMethod(
                        match.Groups["name"].Value,
                        match.Groups["returnType"].Value,
                        match.Groups["params"].Value,
                        source[match.Index..(i + 1)]));
                    break;
                }
            }
        }

        return result;
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var marker = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(marker >= 0, $"未找到方法：{methodName}");

        var start = source.LastIndexOf("public async Task", marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', marker);
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

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ReadSmRunSource(params string[] parts)
    {
        var current = new DirectoryInfo(FindSolutionRoot());
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "src", "SmRun")))
        {
            current = current.Parent;
        }

        var smRunRoot = Path.Combine("E:\\fan\\work\\code\\vscode\\frida_android\\we\\src\\SmRun");
        return File.ReadAllText(Path.Combine(new[] { smRunRoot }.Concat(parts).ToArray()));
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

    private sealed record AsyncMethod(string Name, string ReturnType, string Parameters, string Body);
}
