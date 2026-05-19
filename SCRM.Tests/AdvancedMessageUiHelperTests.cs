using System.Reflection;
using SCRM.API.Models.Entities;
using SCRM.UI.Components.Pages;

namespace SCRM.Tests;

/// <summary>
/// IM 高级消息卡片展示 helper 回归测试。
/// <para>当前 helper 是页面私有静态方法，本测试通过反射固化关键兜底行为，不改变生产代码访问级别。</para>
/// </summary>
public sealed class AdvancedMessageUiHelperTests
{
    private static readonly Type ChatPaneType = typeof(ImCenterChatPane);

    [Theory]
    [InlineData("KefuNameCard", "客服名片")]
    [InlineData("QiyeNameCard", "企微名片")]
    [InlineData("FinderFeed", "视频号")]
    [InlineData("FinderLive", "视频号直播")]
    [InlineData("RoomLiving", "群直播")]
    public void GetAdvancedKindLabel_ShouldReturnStableChineseLabel(string semanticKind, string expectedLabel)
    {
        var actual = InvokeHelper<string>("GetAdvancedKindLabel", semanticKind);

        Assert.Equal(expectedLabel, actual);
    }

    [Fact]
    public void GetAdvancedTitle_ShouldFallbackToFinderNickname()
    {
        var advanced = CreateAdvanced("FinderFeed");
        var finder = CreateNested("AdvancedFinderDisplay");
        SetProperty(finder, "Nickname", "测试视频号昵称");
        SetProperty(advanced, "Finder", finder);

        var actual = InvokeHelper<string>("GetAdvancedTitle", advanced);

        Assert.Equal("测试视频号昵称", actual);
    }

    [Fact]
    public void GetAdvancedDescription_ShouldFallbackToLocationTitleWhenLabelMissing()
    {
        var advanced = CreateAdvanced("Location");
        var location = CreateNested("AdvancedLocationDisplay");
        SetProperty(location, "Title", "测试位置标题");
        SetProperty(advanced, "Location", location);

        var actual = InvokeHelper<string>("GetAdvancedDescription", advanced);

        Assert.Equal("测试位置标题", actual);
    }

    [Fact]
    public void GetAdvancedDescription_ShouldFallbackToNameCardAliasWhenUsernameMissing()
    {
        var advanced = CreateAdvanced("NameCard");
        var nameCard = CreateNested("AdvancedNameCardDisplay");
        SetProperty(nameCard, "Alias", "alias_sample");
        SetProperty(advanced, "NameCard", nameCard);

        var actual = InvokeHelper<string>("GetAdvancedDescription", advanced);

        Assert.Equal("alias_sample", actual);
    }

    [Fact]
    public void GetAdvancedMeta_ShouldIncludeFinderDebugFields()
    {
        var advanced = CreateAdvanced("FinderFeed");
        SetProperty(advanced, "MsgSvrId", 123456789L);
        SetProperty(advanced, "OriginalMsgType", 754974769);

        var finder = CreateNested("AdvancedFinderDisplay");
        SetProperty(finder, "FeedId", "feed-v287");
        SetProperty(finder, "NonceId", "nonce-v287");
        SetProperty(finder, "Username", "finder_user_v287");
        SetProperty(advanced, "Finder", finder);

        var actual = InvokeHelper<string>("GetAdvancedMeta", advanced);

        Assert.Contains("MsgSvrId：123456789", actual);
        Assert.Contains("原始类型：754974769", actual);
        Assert.Contains("FeedId：feed-v287", actual);
        Assert.Contains("NonceId：nonce-v287", actual);
        Assert.Contains("FinderUser：finder_user_v287", actual);
    }

    [Fact]
    public void GetAdvancedMeta_ShouldIncludeFileDebugFields()
    {
        var advanced = CreateAdvanced("File");
        SetProperty(advanced, "OriginalMsgType", 1048625);
        SetProperty(advanced, "Md5", "0123456789abcdef");
        SetProperty(advanced, "FileSize", 1536L);

        var actual = InvokeHelper<string>("GetAdvancedMeta", advanced);

        Assert.Contains("原始类型：1048625", actual);
        Assert.Contains("Md5：0123456789abcdef", actual);
        Assert.Contains("大小：", actual);
    }

    [Theory]
    [InlineData("File", false)]
    [InlineData("Emoji", false)]
    [InlineData("Link", true)]
    [InlineData("Quote", true)]
    public void IsAdvancedCardKind_ShouldKeepMediaKindsOutsideCardPrimarySelection(string kindName, bool expected)
    {
        var actual = InvokeHelper<bool>("IsAdvancedCardKind", ParseChatMessageKind(kindName));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("File", "https://static.example.local/file.pdf", false)]
    [InlineData("Emoji", "https://static.example.local/emoji.gif", false)]
    [InlineData("File", "", true)]
    [InlineData("Emoji", "", true)]
    [InlineData("Link", "", true)]
    public void ShouldRenderAdvancedCard_ShouldPreferDirectMediaForFileAndEmoji(string kindName, string mediaUrl, bool expected)
    {
        var display = CreateNested("ChatMessageDisplay");
        SetProperty(display, "Kind", ParseChatMessageKind(kindName));
        SetProperty(display, "MediaUrl", mediaUrl);
        SetProperty(display, "Advanced", CreateAdvanced(kindName));

        var actual = InvokeHelper<bool>("ShouldRenderAdvancedCard", display);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldRenderAdvancedCard_ShouldReturnFalseWhenAdvancedMissing()
    {
        var display = CreateNested("ChatMessageDisplay");
        SetProperty(display, "Kind", ParseChatMessageKind("Link"));

        var actual = InvokeHelper<bool>("ShouldRenderAdvancedCard", display);

        Assert.False(actual);
    }

    [Fact]
    public void ResolveAdvancedMessageDisplay_ShouldPreferLatestBeforeKindExtension()
    {
        var extensions = new List<MessageExtensionViewDto>
        {
            CreateExtension("advanced_content:weapp", """{"SemanticKind":"WeApp","Title":"kind扩展标题"}"""),
            CreateExtension("advanced_content_latest", """{"SemanticKind":"Quote","Title":"latest扩展标题"}""")
        };

        var actual = InvokeNullableHelper("ResolveAdvancedMessageDisplay", extensions);

        Assert.NotNull(actual);
        Assert.Equal("Quote", GetProperty<string>(actual, "SemanticKind"));
        Assert.Equal("latest扩展标题", GetProperty<string>(actual, "Title"));
    }

    [Fact]
    public void ResolveAdvancedMessageDisplay_ShouldFallbackToKindExtensionWhenLatestMalformed()
    {
        var extensions = new List<MessageExtensionViewDto>
        {
            CreateExtension("advanced_content_latest", "{not-json"),
            CreateExtension("advanced_content:finderfeed", """{"SemanticKind":"FinderFeed","Title":"视频号标题"}""")
        };

        var actual = InvokeNullableHelper("ResolveAdvancedMessageDisplay", extensions);

        Assert.NotNull(actual);
        Assert.Equal("FinderFeed", GetProperty<string>(actual, "SemanticKind"));
        Assert.Equal("视频号标题", GetProperty<string>(actual, "Title"));
    }

    [Fact]
    public void ResolveAdvancedMessageDisplay_ShouldFallbackToKindExtensionWhenLatestUnknown()
    {
        var extensions = new List<MessageExtensionViewDto>
        {
            CreateExtension("advanced_content_latest", """{"SemanticKind":"Unknown","Title":"不可展示"}"""),
            CreateExtension("advanced_content:luckymoney", """{"SemanticKind":"LuckyMoney","Title":"红包标题"}""")
        };

        var actual = InvokeNullableHelper("ResolveAdvancedMessageDisplay", extensions);

        Assert.NotNull(actual);
        Assert.Equal("LuckyMoney", GetProperty<string>(actual, "SemanticKind"));
        Assert.Equal("红包标题", GetProperty<string>(actual, "Title"));
    }

    [Fact]
    public void ResolveAdvancedMessageDisplay_ShouldIgnoreHistoryWhenLatestAndKindMissing()
    {
        var extensions = new List<MessageExtensionViewDto>
        {
            CreateExtension("advanced_content_history", """[{"SemanticKind":"Quote","Title":"历史标题"}]""")
        };

        var actual = InvokeNullableHelper("ResolveAdvancedMessageDisplay", extensions);

        Assert.Null(actual);
    }

    private static object CreateAdvanced(string semanticKind)
    {
        var advanced = CreateNested("AdvancedMessageDisplay");
        SetProperty(advanced, "SemanticKind", semanticKind);
        return advanced;
    }

    private static object CreateNested(string nestedTypeName)
    {
        var type = ChatPaneType.GetNestedType(nestedTypeName, BindingFlags.NonPublic);
        Assert.NotNull(type);

        var instance = Activator.CreateInstance(type, nonPublic: true);
        Assert.NotNull(instance);
        return instance;
    }

    private static object ParseChatMessageKind(string kindName)
    {
        var type = ChatPaneType.GetNestedType("ChatMessageKind", BindingFlags.NonPublic);
        Assert.NotNull(type);
        return Enum.Parse(type, kindName);
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);

        property.SetValue(target, value);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);

        var value = property.GetValue(target);
        return Assert.IsType<T>(value);
    }

    private static MessageExtensionViewDto CreateExtension(string key, string value)
    {
        return new MessageExtensionViewDto
        {
            extensionKey = key,
            extensionValue = value,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }

    private static T InvokeHelper<T>(string methodName, params object?[] args)
    {
        var method = ChatPaneType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var value = method.Invoke(null, args);
        return Assert.IsType<T>(value);
    }

    private static object? InvokeNullableHelper(string methodName, params object?[] args)
    {
        var method = ChatPaneType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return method.Invoke(null, args);
    }
}
