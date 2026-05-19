using System.Reflection;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Services.Netty.Handlers;
using SCRM.API.Services.Netty.Parsing;

namespace SCRM.Tests;

/// <summary>
/// 高级聊天消息结构化解析回归测试。
/// <para>样本只使用脱敏 JSON，覆盖 62203 对齐过程中容易回退的类型分流。</para>
/// </summary>
public sealed class AdvancedMessageContentParserTests
{
    [Theory]
    [InlineData("appmsg-type6-file", 6, """
        {"Type":6,"Title":"报价单.pdf","FileExt":"pdf","FileSize":4096,"Url":"https://example.invalid/file"}
        """, "File", null)]
    [InlineData("appmsg-type74-file", 6, """
        {"Type":74,"Title":"新版附件.zip","FileExt":"zip","FileSize":8192,"Url":"https://example.invalid/file74"}
        """, "File", null)]
    [InlineData("appmsg-type33-weapp", 6, """
        {"Type":33,"Title":"小程序卡片","AppId":"wx123","PagePath":"pages/index/index","Thumb":"https://example.invalid/thumb.jpg"}
        """, "WeApp", null)]
    [InlineData("appmsg-type36-weapp", 6, """
        {"Type":36,"Title":"小程序卡片二","AppId":"wx456","PagePath":"pages/detail/index","Thumb":"https://example.invalid/thumb2.jpg"}
        """, "WeApp", null)]
    [InlineData("finder-id-nonce-media-list", 6, """
        {"id":"123456789","nonceId":"nonce-v285","nickname":"测试视频号","username":"finder_user","des":"视频号内容","mediaList":[{"url":"https://example.invalid/video.mp4"}]}
        """, "FinderFeed", "123456789")]
    [InlineData("quote-json", 22, """
        {"QuoteSvrId":998877,"QuoteType":1,"QuoteUser":"wxid_sample","Content":"引用内容","DisplayName":"示例用户"}
        """, "Quote", null)]
    [InlineData("finder-live-json", 28, """
        {"liveId":"live-v285","feedId":"feed-live-v285","nonceId":"live-nonce","finderUsername":"finder_live_user","coverUrl":"https://example.invalid/live.jpg"}
        """, "FinderLive", "feed-live-v285")]
    [InlineData("emoji-raw-file-cache", 14, """
        {"Md5":"0123456789abcdef","searchCachePath":"/cache/file/report.docx","Title":"report.docx","FileExt":"docx"}
        """, "File", null)]
    [InlineData("emoji-raw-emoji-cache", 14, """
        {"Md5":"abcdef0123456789","searchCachePath":"/cache/emoji/emoji.dat","Thumb":"/cache/emoji/thumb.png"}
        """, "Emoji", null)]
    [InlineData("emoji-raw-emoji-cache-count-only", 14, """
        {"Md5":"abcdef0123456789abcdef0123456789","searchCachePath":"/cache/emoji/emoji.dat","searchResultCount":123}
        """, "Emoji", null)]
    public void Parse_ShouldResolveAdvancedMessageSemanticKind(
        string caseName,
        short messageType,
        string json,
        string expectedSemanticKind,
        string? expectedFeedId)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));

        var parsed = AdvancedMessageContentParser.Parse(
            messageType,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: null,
            msgSvrId: 10001,
            localMsgId: 20001);

        Assert.NotNull(parsed);
        Assert.Equal(expectedSemanticKind, parsed.SemanticKind);

        if (expectedFeedId != null)
        {
            Assert.NotNull(parsed.Finder);
            Assert.Equal(expectedFeedId, parsed.Finder.FeedId);
        }
    }

    [Theory]
    [InlineData("file-cache-wire-thumb-size-document-path", 14, """
        {"Md5":"0123456789abcdef0123456789abcdef","Thumb":"/cache/file/report.pdf","Size":123456}
        """, "File", "/cache/file/report.pdf", 123456L)]
    [InlineData("file-cache-wire-thumb-size-emoji-cache", 14, """
        {"Md5":"abcdef0123456789abcdef0123456789","Thumb":"/cache/emoji/emoji.dat","Size":123456}
        """, "Emoji", "/cache/emoji/emoji.dat", 123456L)]
    [InlineData("file-cache-wire-thumb-size-wait-thumb", 14, """
        {"Md5":"abcdef0123456789abcdef0123456789","Thumb":"TODO-Wait-Thumb","Size":123456}
        """, "Emoji", "TODO-Wait-Thumb", 123456L)]
    public void Parse_ShouldKeepFileCacheWireThumbSizeBoundary(
        string caseName,
        short messageType,
        string json,
        string expectedSemanticKind,
        string expectedThumbUrl,
        long expectedFileSize)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));

        var parsed = AdvancedMessageContentParser.Parse(
            messageType,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 1048625,
            msgSvrId: 10011,
            localMsgId: 20011);

        Assert.NotNull(parsed);
        Assert.Equal(expectedSemanticKind, parsed.SemanticKind);
        Assert.Equal(expectedThumbUrl, parsed.ThumbUrl);
        Assert.Equal(expectedFileSize, parsed.FileSize);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Md5));
    }

    [Theory]
    [InlineData("<msg><appmsg><title>有效 XML</title></appmsg></msg>", true)]
    [InlineData("<?xml version=\"1.0\"?><msg><appmsg /></msg>", true)]
    [InlineData("<msg><appmsg><title>A & B</title></appmsg></msg>", false)]
    [InlineData("<msg><appmsg><title>缺少根闭合</title></appmsg>", false)]
    [InlineData("普通文本 <not-xml>", false)]
    public void XmlEnvelopePrecheck_ShouldFilterMalformedXmlBeforeXDocumentParse(
        string candidate,
        bool expected)
    {
        var method = typeof(AdvancedMessageContentParser).GetMethod(
            "LooksLikeXmlEnvelope",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var actual = Assert.IsType<bool>(method.Invoke(null, new object?[] { candidate }));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Parse_ShouldFallbackWhenXmlLikeContentIsMalformed()
    {
        const string malformedXml = "<msg><appmsg><title>A & B</title></appmsg></msg>";

        var parsed = AdvancedMessageContentParser.Parse(
            6,
            malformedXml,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: null,
            msgSvrId: 10012,
            localMsgId: 20012);

        Assert.NotNull(parsed);
        Assert.Equal("Link", parsed.SemanticKind);
        Assert.Equal("Plain", parsed.RawKind);
        Assert.Contains("A & B", parsed.Title);
    }

    [Fact]
    public void Parse_ShouldResolveFinderFeedXmlAliases()
    {
        const string xml = """
            <msg>
              <appmsg>
                <type>51</type>
                <finderFeed>
                  <objectId>feed-xml-001</objectId>
                  <objectNonceId>nonce-xml-001</objectNonceId>
                  <finderUsername>finder_xml_user</finderUsername>
                  <nickname>XML视频号</nickname>
                  <thumbUrl>https://example.invalid/finder-thumb.jpg</thumbUrl>
                  <desc>XML视频号内容</desc>
                </finderFeed>
              </appmsg>
            </msg>
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            24,
            content: null,
            contentXml: xml,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 754974769,
            msgSvrId: 10002,
            localMsgId: 20002);

        Assert.NotNull(parsed);
        Assert.Equal("FinderFeed", parsed.SemanticKind);
        Assert.NotNull(parsed.Finder);
        Assert.Equal("feed-xml-001", parsed.Finder.FeedId);
        Assert.Equal("nonce-xml-001", parsed.Finder.NonceId);
        Assert.Equal("finder_xml_user", parsed.Finder.Username);
        Assert.Equal("https://example.invalid/finder-thumb.jpg", parsed.Finder.CoverUrl);
    }

    [Fact]
    public void Parse_ShouldResolveFinderFeedXmlAliasesWithoutWrapper()
    {
        const string xml = """
            <msg>
              <appmsg>
                <type>51</type>
                <objectId>feed-direct-001</objectId>
                <objectNonceId>nonce-direct-001</objectNonceId>
                <finderUsername>finder_direct_user</finderUsername>
                <coverUrl>https://example.invalid/direct-cover.jpg</coverUrl>
                <desc>没有 finderFeed 外层节点的视频号内容</desc>
              </appmsg>
            </msg>
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            24,
            content: null,
            contentXml: xml,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 754974769,
            msgSvrId: 10012,
            localMsgId: 20012);

        Assert.NotNull(parsed);
        Assert.Equal("FinderFeed", parsed.SemanticKind);
        Assert.NotNull(parsed.Finder);
        Assert.Equal("feed-direct-001", parsed.Finder.FeedId);
        Assert.Equal("nonce-direct-001", parsed.Finder.NonceId);
        Assert.Equal("finder_direct_user", parsed.Finder.Username);
        Assert.Equal("https://example.invalid/direct-cover.jpg", parsed.Finder.CoverUrl);
    }

    [Fact]
    public void Parse_ShouldResolveFinderLiveXmlAliases()
    {
        const string xml = """
            <msg>
              <appmsg>
                <finderLive>
                  <finderLiveID>live-xml-001</finderLiveID>
                  <finderObjectID>feed-live-xml-001</finderObjectID>
                  <finderNonceID>nonce-live-xml-001</finderNonceID>
                  <finderUsername>finder_live_xml</finderUsername>
                  <fullCoverUrl>https://example.invalid/live-cover.jpg</fullCoverUrl>
                </finderLive>
              </appmsg>
            </msg>
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            28,
            content: null,
            contentXml: xml,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 973078577,
            msgSvrId: 10003,
            localMsgId: 20003);

        Assert.NotNull(parsed);
        Assert.Equal("FinderLive", parsed.SemanticKind);
        Assert.NotNull(parsed.Finder);
        Assert.Equal("live-xml-001", parsed.Finder.LiveId);
        Assert.Equal("feed-live-xml-001", parsed.Finder.FeedId);
        Assert.Equal("nonce-live-xml-001", parsed.Finder.NonceId);
        Assert.Equal("https://example.invalid/live-cover.jpg", parsed.Finder.CoverUrl);
    }

    [Fact]
    public void Parse_ShouldKeepRoomLivingWhenMessageTypeIsRoomLiving()
    {
        const string json = """
            {
              "liveId":"room-live-001",
              "feedId":"room-feed-001",
              "nonceId":"room-nonce-001",
              "finderUsername":"room_live_user",
              "coverUrl":"https://example.invalid/room-live.jpg",
              "description":"群直播开始了"
            }
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            25,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 855638065,
            msgSvrId: 10004,
            localMsgId: 20004);

        Assert.NotNull(parsed);
        Assert.Equal("RoomLiving", parsed.SemanticKind);
        Assert.NotNull(parsed.Finder);
        Assert.Equal("room-live-001", parsed.Finder.LiveId);
        Assert.Equal("room-feed-001", parsed.Finder.FeedId);
    }

    [Fact]
    public void Parse_ShouldKeepRoomLivingXmlWhenMessageTypeIsRoomLiving()
    {
        const string xml = """
            <msg>
              <appmsg>
                <liveId>room-live-xml-001</liveId>
                <finderLiveID>room-live-alias-001</finderLiveID>
                <feedId>room-feed-xml-001</feedId>
                <objectNonceId>room-nonce-xml-001</objectNonceId>
                <finderUsername>room_live_xml_user</finderUsername>
                <thumbUrl>https://example.invalid/room-live-xml.jpg</thumbUrl>
              </appmsg>
            </msg>
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            25,
            content: null,
            contentXml: xml,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 855638065,
            msgSvrId: 10013,
            localMsgId: 20013);

        Assert.NotNull(parsed);
        Assert.Equal("RoomLiving", parsed.SemanticKind);
        Assert.NotNull(parsed.Finder);
        Assert.Equal("room-feed-xml-001", parsed.Finder.FeedId);
        Assert.Equal("room-live-xml-001", parsed.Finder.LiveId);
        Assert.Equal("room-nonce-xml-001", parsed.Finder.NonceId);
    }

    [Fact]
    public void Parse_ShouldNotTreatPlainLinkAsPayment()
    {
        const string json = """
            {
              "Type":5,
              "Title":"普通网页链接",
              "Description":"只包含标题、摘要和 URL，不应被识别为支付",
              "Url":"https://example.invalid/article?id=100",
              "Thumb":"https://example.invalid/thumb.jpg"
            }
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            5,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 49,
            msgSvrId: 10014,
            localMsgId: 20014);

        Assert.NotNull(parsed);
        Assert.Equal("Link", parsed.SemanticKind);
        Assert.Null(parsed.Payment);
        Assert.Equal("普通网页链接", parsed.Title);
        Assert.Equal("https://example.invalid/article?id=100", parsed.Url);
    }

    [Fact]
    public void Parse_ShouldResolveMoneyTransAndroidJsonFields()
    {
        const string json = """
            {
              "feedbackDescription":"￥88.00",
              "dataInvalidTime":1770000000,
              "analysisKey":"transfer-abc-001",
              "paymentSubType":1,
              "payerInfo":"wxid_payer",
              "receiverInfo":"wxid_receiver",
              "analysisTitle":"给你转账"
            }
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            12,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 419430449,
            msgSvrId: 10006,
            localMsgId: 20006);

        Assert.NotNull(parsed);
        Assert.Equal("MoneyTrans", parsed.SemanticKind);
        Assert.Equal("给你转账", parsed.Title);
        Assert.Equal("￥88.00", parsed.Description);
        Assert.NotNull(parsed.Payment);
        Assert.Equal("￥88.00", parsed.Payment.FeeDescription);
        Assert.Equal("transfer-abc-001", parsed.Payment.Key);
        Assert.Equal("transfer-abc-001", parsed.Payment.TransferId);
        Assert.Equal("1", parsed.Payment.HbType);
    }

    [Fact]
    public void Parse_ShouldResolveMoneyTransXmlPayMemoAndFeedesc()
    {
        const string xml = """
            <msg>
              <appmsg>
                <paysubtype>1</paysubtype>
                <pay_memo>给你转账</pay_memo>
                <feedesc>￥66.00</feedesc>
                <transferid>transfer-xml-001</transferid>
                <payer_username>wxid_payer</payer_username>
                <receiver_username>wxid_receiver</receiver_username>
                <invalidtime>1770000001</invalidtime>
              </appmsg>
            </msg>
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            12,
            content: null,
            contentXml: xml,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 419430449,
            msgSvrId: 10007,
            localMsgId: 20007);

        Assert.NotNull(parsed);
        Assert.Equal("MoneyTrans", parsed.SemanticKind);
        Assert.Equal("给你转账", parsed.Title);
        Assert.Equal("￥66.00", parsed.Description);
        Assert.NotNull(parsed.Payment);
        Assert.Equal("￥66.00", parsed.Payment.FeeDescription);
        Assert.Equal("transfer-xml-001", parsed.Payment.Key);
        Assert.Equal("transfer-xml-001", parsed.Payment.TransferId);
        Assert.Equal("1", parsed.Payment.HbType);
    }

    [Fact]
    public void Parse_ShouldKeepLuckyMoneyJsonFields()
    {
        const string json = """
            {
              "Title":"恭喜发财，大吉大利",
              "Key":"weixin://wxpay/nativeurl?total_num=1",
              "InvalidTime":1770000002,
              "hbtype":0,
              "totalNum":1
            }
            """;

        var parsed = AdvancedMessageContentParser.Parse(
            11,
            json,
            contentXml: null,
            ext: null,
            sourceNotice: "unit-test",
            originalMsgType: 10000,
            msgSvrId: 10008,
            localMsgId: 20008);

        Assert.NotNull(parsed);
        Assert.Equal("LuckyMoney", parsed.SemanticKind);
        Assert.Equal("恭喜发财，大吉大利", parsed.Title);
        Assert.NotNull(parsed.Payment);
        Assert.Equal("weixin://wxpay/nativeurl?total_num=1", parsed.Payment.Key);
        Assert.Equal("0", parsed.Payment.HbType);
        Assert.Equal(1, parsed.Payment.TotalNum);
    }

    [Theory]
    [InlineData(42, EnumContentType.NameCard)]
    [InlineData(66, EnumContentType.QiyeNameCard)]
    [InlineData(67, EnumContentType.KefuNameCard)]
    [InlineData(1048625, EnumContentType.Emoji)]
    [InlineData(754974769, EnumContentType.ShiPinHao)]
    [InlineData(822083633, EnumContentType.QuoteMsg)]
    [InlineData(855638065, EnumContentType.RoomLiving)]
    [InlineData(973078577, EnumContentType.FinderLive)]
    [InlineData(64, EnumContentType.System)]
    [InlineData(268445456, EnumContentType.System)]
    [InlineData(268445458, EnumContentType.System)]
    [InlineData(1107296305, EnumContentType.RoomManage)]
    public void NormalizeOriginalContentType_ShouldMapNewWechatRawTypes(
        int rawMsgType,
        EnumContentType expectedContentType)
    {
        var method = typeof(ChatMessageHandler).GetMethod(
            "NormalizeOriginalContentType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var actual = Assert.IsType<EnumContentType>(method.Invoke(null, new object[] { rawMsgType }));
        Assert.Equal(expectedContentType, actual);
    }
}
