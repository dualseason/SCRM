using System.Reflection;
using SCRM.API.Services.Data;

namespace SCRM.Tests;

/// <summary>
/// 消息扩展回传白名单回归测试。
/// <para>确保高级消息扩展能被列表查询富化到 Message.messageExtensions，供 IM 页面读取。</para>
/// </summary>
public sealed class AdvancedMessageExtensionVisibilityTests
{
    [Theory]
    [InlineData("advanced_content_latest")]
    [InlineData("advanced_content_history")]
    [InlineData("advanced_content:File")]
    [InlineData("advanced_content:file")]
    [InlineData("advanced_content:FinderFeed")]
    [InlineData("advanced_content:Quote")]
    public void IsVisibleMessageExtensionKey_ShouldAllowAdvancedContentKeys(string extensionKey)
    {
        var actual = InvokeIsVisibleMessageExtensionKey(extensionKey);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown_extension")]
    [InlineData("media_raw_unrelated")]
    [InlineData("advanced_content")]
    [InlineData("advanced_content_file")]
    [InlineData("advanced_contentFile")]
    public void IsVisibleMessageExtensionKey_ShouldRejectUnrelatedKeys(string extensionKey)
    {
        var actual = InvokeIsVisibleMessageExtensionKey(extensionKey);

        Assert.False(actual);
    }

    private static bool InvokeIsVisibleMessageExtensionKey(string extensionKey)
    {
        var method = typeof(DbHelper).GetMethod(
            "IsVisibleMessageExtensionKey",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var value = method.Invoke(null, new object?[] { extensionKey });
        return Assert.IsType<bool>(value);
    }
}
