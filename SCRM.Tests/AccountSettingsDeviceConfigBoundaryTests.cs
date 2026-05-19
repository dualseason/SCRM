namespace SCRM.Tests;

/// <summary>
/// 账号自动化设置与 Android 设备配置边界防回归测试。
/// <para>服务端 AutoAccept* 属于账号自动化策略；Android SetConfigTask 只接受 62203 已知设备配置键，例如 silentAccept。</para>
/// </summary>
public sealed class AccountSettingsDeviceConfigBoundaryTests
{
    [Fact]
    public void KnownDeviceConfigKeys_ShouldContainSilentAcceptButNotServerAutomationKeys()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var knownBoolBlock = ExtractFieldInitializer(source, "private static readonly HashSet<string> KnownDeviceBoolConfigKeys");

        Assert.Contains("\"silentAccept\"", knownBoolBlock);
        Assert.Contains("\"fastSend\"", knownBoolBlock);
        Assert.Contains("\"addInWw\"", knownBoolBlock);
        Assert.DoesNotContain("AutoAcceptFriendRequest", knownBoolBlock);
        Assert.DoesNotContain("AutoAcceptLuckyMoney", knownBoolBlock);
        Assert.DoesNotContain("AutoLikeMoments", knownBoolBlock);
    }

    [Fact]
    public void PushAccountSettings_ShouldNotSendUnknownAndroidConfigKeys()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var method = ExtractMethod(source, "public Task<bool> PushAccountSettingsAsync");

        Assert.Contains("服务端账号自动化策略，不是 Android SetConfigTask", method);
        Assert.Contains("silentAccept 才是 Android 侧静默通过", method);
        Assert.Contains("return Task.FromResult(true);", method);
        Assert.DoesNotContain("SendSetConfigTaskAsync", method);
        Assert.DoesNotContain("boolConfs.Add(\"AutoAcceptFriendRequest\"", method);
        Assert.DoesNotContain("boolConfs.Add(\"AutoAcceptLuckyMoney\"", method);
        Assert.DoesNotContain("boolConfs.Add(\"AutoLikeMoments\"", method);
    }

    [Fact]
    public void UpdateAccountSettings_ShouldPersistServerSettingsAndTreatPushAsBestEffort()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var method = ExtractMethod(source, "public async Task<bool> UpdateAccountSettingsAsync");

        Assert.Contains("account.settings = JsonSerializer.Serialize(settings);", method);
        Assert.Contains("await db.SaveChangesAsync();", method);
        Assert.Contains("账号配置已先保存到数据库，避免推送失败影响持久化", method);
        Assert.Contains("PushAccountSettingsAsync(account.clientUuid, settings)", method);
    }

    [Fact]
    public void SetDeviceConfig_ShouldStillNormalizeOnlyKnown62203Keys()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SetDeviceConfigAsync");

        Assert.Contains("NormalizeDeviceConfigMap(config.BoolConfs, KnownDeviceBoolConfigKeys)", method);
        Assert.Contains("NormalizeDeviceConfigMap(config.IntConfs, KnownDeviceIntConfigKeys)", method);
        Assert.Contains("NormalizeDeviceConfigMap(config.StrConfs, KnownDeviceStrConfigKeys)", method);
        Assert.Contains("没有有效的 62203 配置键可下发", method);
        Assert.Contains("_clientTaskService.SendSetConfigTaskAsync(connectionId, boolConfs, intConfs, strConfs)", method);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string signaturePrefix)
    {
        var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法：{signaturePrefix}");
        return ExtractBlockFromStart(source, start, signaturePrefix);
    }

    private static string ExtractFieldInitializer(string source, string signaturePrefix)
    {
        var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到字段：{signaturePrefix}");
        return ExtractBlockFromStart(source, start, signaturePrefix);
    }

    private static string ExtractBlockFromStart(string source, int start, string label)
    {
        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到块起点：{label}");

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

        throw new InvalidOperationException($"块未闭合：{label}");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("未找到 SCRM.SOLUTION.sln，无法定位 SCRM 源码。");
    }
}
