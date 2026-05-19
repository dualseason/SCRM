using SCRM.SHARED.Utils;

namespace SCRM.Tests;

/// <summary>
/// 朋友圈正文提取工具回归测试。
/// </summary>
public sealed class MomentContentExtractorTests
{
    [Theory]
    [InlineData("<contentDesc>今天的朋友圈正文</contentDesc>", "今天的朋友圈正文")]
    [InlineData("<msg><contentdesc><![CDATA[带 CDATA 的正文]]></contentdesc></msg>", "带 CDATA 的正文")]
    [InlineData("<msg contentDesc=\"属性正文\"></msg>", "属性正文")]
    [InlineData("&lt;msg&gt;&lt;desc&gt;HTML 转义正文&lt;/desc&gt;&lt;/msg&gt;", "HTML 转义正文")]
    [InlineData("<msg><desc>Tom & Jerry</desc></msg>", "Tom & Jerry")]
    [InlineData("<sns-ext:contentDesc><![CDATA[带命名前缀的正文]]></sns-ext:contentDesc>", "带命名前缀的正文")]
    public void ExtractTextFromXml_ShouldExtractKnownMomentText(string xml, string expected)
    {
        var actual = MomentContentExtractor.ExtractTextFromXml(xml);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("普通朋友圈文本")]
    [InlineData("1 < 2 且 3 > 2")]
    [InlineData("<msg><desc>被截断")]
    [InlineData("<msg><desc>闭合错位</msg>")]
    [InlineData("{\"text\":\"json 不是朋友圈 XML\"}")]
    public void ExtractTextFromXml_ShouldIgnorePlainOrBrokenText(string text)
    {
        var actual = MomentContentExtractor.ExtractTextFromXml(text);

        Assert.Equal(string.Empty, actual);
    }
}
