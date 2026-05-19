namespace SCRM.Tests;

/// <summary>
/// 全局微信敏感操作权限配置 UI 防回归测试。
/// <para>全局 Settings 页面、单设备配置页面和后端广播白名单必须对 wx_del_conv 保持一致，避免隐藏配置造成误解。</para>
/// </summary>
public sealed class SensitivePermissionSettingsUiTests
{
    [Fact]
    public void SettingsPage_ShouldExposeWxDelConvGlobalSwitchWithScopeWarning()
    {
        var source = ReadSource("SCRM.UI", "Components", "Pages", "Settings.razor");
        var permissionTab = ExtractBlockFromMarker(source, "<RadzenTabsItem Text=\"微信权限控制 (Permissions)\">");

        Assert.Contains("本页保存后会作为全局默认配置推送到所有在线客户端", permissionTab);
        Assert.Contains("如只想调整单台设备，请使用“设备配置”页面", permissionTab);
        Assert.Contains("允许删除会话 (wx_del_conv)", permissionTab);
        Assert.Contains("@bind-Value=@configModel.wx_del_conv", permissionTab);
        Assert.Contains("Name=\"wx_del_conv\"", permissionTab);
        Assert.Contains("rconversation", permissionTab);

        Assert.True(permissionTab.IndexOf("允许拉黑 (wx_can_block)", StringComparison.Ordinal) < permissionTab.IndexOf("允许删除会话 (wx_del_conv)", StringComparison.Ordinal));
        Assert.True(permissionTab.IndexOf("允许删除会话 (wx_del_conv)", StringComparison.Ordinal) < permissionTab.IndexOf("允许退群 (wx_can_exitGroup)", StringComparison.Ordinal));
    }

    [Fact]
    public void SystemConfigModelAndGlobalBroadcastWhitelist_ShouldContainWxDelConv()
    {
        var model = ReadSource("SCRM.SHARED", "Models", "SystemConfigModel.cs");
        var controller = ReadSource("SCRM.API", "Controllers", "SystemConfigController.cs");
        var legacyBoolKeys = ExtractBlockFromMarker(controller, "private static readonly HashSet<string> LegacyBroadcastBoolKeys");

        Assert.Contains("public bool wx_del_conv { get; set; } = false", model);
        Assert.Contains("\"wx_del_conv\"", legacyBoolKeys);
        Assert.Contains("AppendLegacyAllowedConfig(msg, cfg.key, cfg.value, \"Batch Update\")", controller);
    }

    [Fact]
    public void DeviceConfigPage_ShouldStillExposePerDeviceWxDelConvSwitch()
    {
        var source = ReadSource("SCRM.UI", "Components", "Pages", "DeviceConfig.razor");
        var boolRows = ExtractBlockFromMarker(source, "private readonly List<BoolConfigRow> _boolRows");

        Assert.Contains("new(\"wx_del_conv\", \"允许删除会话\")", boolRows);
        Assert.Contains("new(\"wx_can_delete\", \"允许删除联系人\")", boolRows);
        Assert.Contains("new(\"wx_can_block\", \"允许拉黑联系人\")", boolRows);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractBlockFromMarker(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到标记：{marker}");

        var braceStart = source.IndexOf('{', start);
        var angleStart = source.IndexOf('>', start);
        var blockStart = braceStart >= 0 && (angleStart < 0 || braceStart < angleStart) ? braceStart : angleStart;
        Assert.True(blockStart >= 0, $"未找到块起点：{marker}");

        if (blockStart == braceStart)
        {
            return ExtractBraceBlock(source, start, marker);
        }

        var tabEnd = source.IndexOf("</RadzenTabsItem>", blockStart, StringComparison.Ordinal);
        if (tabEnd >= 0)
        {
            return source[start..(tabEnd + "</RadzenTabsItem>".Length)];
        }

        return source[start..];
    }

    private static string ExtractBraceBlock(string source, int start, string label)
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
