namespace SCRM.Tests;

/// <summary>
/// 手机号批量加好友任务防回归测试。
/// <para>62203 Android 执行层当前只读取 AddFriendsTask.Phones[0]，所以服务端公开入口必须把多手机号拆成多个 1072 任务。</para>
/// </summary>
public sealed class AddFriendsByPhoneBatchDispatchTests
{
    [Fact]
    public void ServerDeviceCommandService_ShouldDispatchMultiplePhonesOneByOne()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var method = ExtractMethod(source, "public async Task<TaskResult> AddFriendsByPhoneAsync");

        Assert.Contains("var normalizedPhones = (phones ?? Enumerable.Empty<string>())", method);
        Assert.Contains("Distinct(StringComparer.OrdinalIgnoreCase)", method);
        Assert.Contains("normalizedPhones.Count == 0", method);
        Assert.Contains("GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendsByPhoneAsync))", method);
        Assert.Contains("设备当前没有可用微信账号", method);
        Assert.Contains("normalizedPhones.Count == 1", method);
        Assert.Contains("for (var index = 0; index < normalizedPhones.Count; index++)", method);
        Assert.Contains("new[] { phone }", method);
        Assert.Contains("ownerWxid);", method);
        Assert.Contains("await Task.Delay(500);", method);
        Assert.Contains("手机号加好友已逐条下发", method);
        Assert.Contains("手机号加好友全部下发失败", method);
        Assert.DoesNotContain("return await _clientTaskService.SendAddFriendsTaskAsync(connectionId, phones", method);
    }

    [Fact]
    public void ClientHub_ShouldReuseServerDeviceCommandServiceForPhoneBatch()
    {
        var source = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var method = ExtractMethod(source, "public async Task<TaskResult> AddFriendsByPhone");

        Assert.Contains("_deviceCommandService.AddFriendsByPhoneAsync(deviceUuid, phones, message, remark, label, permission)", method);
        Assert.DoesNotContain("SendAddFriendsTaskAsync(connectionId, phones", method);
        Assert.DoesNotContain("GetConnectionIdByDeviceUuidAsync(deviceUuid)", method);
    }

    [Fact]
    public void LowLevelClientTaskController_ShouldAlsoSplitMultiplePhones()
    {
        var source = ReadSource("SCRM.API", "Controllers", "ClientTaskController.cs");
        var method = ExtractMethod(source, "public async Task<IActionResult> SendAddFriendsByPhone");

        Assert.Contains("var normalizedPhones = (request.Phones ?? new List<string>())", method);
        Assert.Contains("normalizedPhones.Count == 1", method);
        Assert.Contains("for (var index = 0; index < normalizedPhones.Count; index++)", method);
        Assert.Contains("new[] { phone }", method);
        Assert.Contains("request.WeChatId);", method);
        Assert.Contains("await Task.Delay(500);", method);
        Assert.Contains("低层调试 API 也做逐条下发兜底", method);
        Assert.Contains("手机号加好友已逐条下发", method);
        Assert.DoesNotContain("request.Phones,", method);
    }

    [Fact]
    public void ClientTaskService_ShouldKeepRepeatedProtoSenderButDocumentSingleConsumer()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendsTaskAsync");

        Assert.Contains("当前 Android 执行层只读取 Phones[0]", source);
        Assert.Contains("上层批量应逐个手机号下发", source);
        Assert.Contains("task.Phones.AddRange(normalizedPhones);", method);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", method);
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

    private static string ExtractBlockFromStart(string source, int start, string label)
    {
        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{label}");

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

        throw new InvalidOperationException($"方法体未闭合：{label}");
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
