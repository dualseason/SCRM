using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace SCRM.API.Services.Netty.Parsing;

/// <summary>
/// 高级聊天内容结构化解析器。
/// <para>只做展示语义提取，不修改消息原始类型和正文，避免影响已稳定的收发链路。</para>
/// </summary>
public static class AdvancedMessageContentParser
{
    private const short MessageTypeLink = 6;
    private const short MessageTypeLinkExt = 7;
    private const short MessageTypeFile = 8;
    private const short MessageTypeNameCard = 9;
    private const short MessageTypeLocation = 10;
    private const short MessageTypeLuckyMoney = 11;
    private const short MessageTypeMoneyTrans = 12;
    private const short MessageTypeWeApp = 13;
    private const short MessageTypeEmoji = 14;
    private const short MessageTypeBizLink = 18;
    private const short MessageTypeQuote = 22;
    private const short MessageTypeFinderFeed = 24;
    private const short MessageTypeRoomLiving = 25;
    private const short MessageTypeFinderLive = 28;
    private const short MessageTypeKefuNameCard = 29;
    private const short MessageTypeQiyeNameCard = 30;

    /// <summary>
    /// 从实时消息正文、原始 XML 和 Ext 中解析高级内容。
    /// </summary>
    public static AdvancedMessageContent? Parse(
        short messageType,
        string? content,
        string? contentXml,
        string? ext,
        string? sourceNotice,
        int? originalMsgType = null,
        long? msgSvrId = null,
        long? localMsgId = null)
    {
        var candidates = new[]
        {
            new CandidateText(content, "Content"),
            new CandidateText(contentXml, "ContentXml"),
            new CandidateText(ext, "Ext")
        };

        foreach (var candidate in candidates)
        {
            if (TryParseJsonCandidate(candidate.Text, out var jsonText, out var document))
            {
                using (document)
                {
                    var parsed = ParseJsonRoot(
                        document.RootElement,
                        jsonText,
                        candidate.Source,
                        messageType,
                        sourceNotice,
                        originalMsgType,
                        msgSvrId,
                        localMsgId);
                    if (parsed != null && !IsUnknown(parsed.SemanticKind))
                    {
                        return parsed;
                    }
                }
            }

            if (TryParseXmlCandidate(candidate.Text, out var xmlText, out var xml))
            {
                var parsed = ParseXmlRoot(
                    xml,
                    xmlText,
                    candidate.Source,
                    messageType,
                    sourceNotice,
                    originalMsgType,
                    msgSvrId,
                    localMsgId);
                if (parsed != null && !IsUnknown(parsed.SemanticKind))
                {
                    return parsed;
                }
            }
        }

        return ParsePlainFallback(
            messageType,
            content,
            sourceNotice,
            originalMsgType,
            msgSvrId,
            localMsgId);
    }

    private static AdvancedMessageContent? ParseJsonRoot(
        JsonElement root,
        string raw,
        string rawKind,
        short messageType,
        string? sourceNotice,
        int? originalMsgType,
        long? msgSvrId,
        long? localMsgId)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = CreateBase(messageType, sourceNotice, originalMsgType, msgSvrId, localMsgId, rawKind, raw);
        FillCommonJsonFields(result, root);

        var hasQuote = LooksLikeQuoteJson(root);
        var hasLocation = LooksLikeLocationJson(root);
        var hasPayment = messageType is MessageTypeLuckyMoney or MessageTypeMoneyTrans || LooksLikePaymentJson(root);
        var hasFinder = LooksLikeFinderJson(root);
        var hasNameCard = LooksLikeNameCardJson(root, messageType);
        var hasAppMsg = LooksLikeAppMsgJson(root, result);
        var hasEmojiCache = LooksLikeEmojiCacheJson(root, result);
        var hasFileCache = LooksLikeFileCacheJson(root, result);
        var hasExplicitFileCacheSignal = LooksLikeExplicitFileCacheJson(root, result);

        if (hasLocation)
        {
            result.SemanticKind = "Location";
            result.Confidence = "High";
            result.Location = BuildLocation(root, result);
            return result;
        }

        if (hasQuote)
        {
            result.SemanticKind = "Quote";
            result.Confidence = messageType == MessageTypeQuote || messageType == MessageTypeRoomLiving ? "High" : "Medium";
            result.Quote = BuildQuote(root, result);
            return result;
        }

        if (hasPayment)
        {
            result.SemanticKind = messageType == MessageTypeLuckyMoney ? "LuckyMoney" : messageType == MessageTypeMoneyTrans ? "MoneyTrans" : "Payment";
            result.Confidence = messageType == MessageTypeLuckyMoney || messageType == MessageTypeMoneyTrans ? "High" : "Medium";
            result.Payment = BuildPayment(root, result);
            return result;
        }

        if (hasFinder)
        {
            result.SemanticKind = ResolveFinderKind(messageType, root);
            result.Confidence = messageType == MessageTypeFinderFeed || messageType == MessageTypeFinderLive || messageType == MessageTypeRoomLiving ? "High" : "Medium";
            result.Finder = BuildFinder(root, result);
            return result;
        }

        if (hasNameCard)
        {
            result.SemanticKind = "NameCard";
            result.Confidence = messageType == MessageTypeNameCard || messageType == MessageTypeKefuNameCard || messageType == MessageTypeQiyeNameCard ? "High" : "Medium";
            result.NameCard = BuildNameCard(root, result);
            return result;
        }

        if (hasAppMsg)
        {
            result.SemanticKind = ResolveAppMsgKind(messageType, result.AppMsgType, result.TypeStr, root);
            result.Confidence = messageType == MessageTypeLink
                || messageType == MessageTypeLinkExt
                || messageType == MessageTypeWeApp
                || messageType == MessageTypeFile
                || messageType == MessageTypeBizLink
                ? "High"
                : "Medium";

            if (result.SemanticKind == "FinderFeed" || result.SemanticKind == "FinderLive" || result.SemanticKind == "RoomLiving")
            {
                result.Finder = BuildFinder(root, result);
            }

            return result;
        }

        if (messageType == MessageTypeEmoji
            && hasEmojiCache
            && hasFileCache
            && !hasExplicitFileCacheSignal)
        {
            result.SemanticKind = "Emoji";
            result.Confidence = "High";
            return result;
        }

        if (hasFileCache)
        {
            result.SemanticKind = "File";
            result.Confidence = messageType == MessageTypeFile ? "High" : "Medium";
            return result;
        }

        if (hasEmojiCache || messageType == MessageTypeEmoji)
        {
            result.SemanticKind = "Emoji";
            result.Confidence = hasEmojiCache ? "High" : "Medium";
            return result;
        }

        result.SemanticKind = ResolveKindFromMessageType(messageType);
        if (!IsUnknown(result.SemanticKind))
        {
            result.Confidence = "Low";
            return result;
        }

        return null;
    }

    private static AdvancedMessageContent? ParseXmlRoot(
        XDocument xml,
        string raw,
        string rawKind,
        short messageType,
        string? sourceNotice,
        int? originalMsgType,
        long? msgSvrId,
        long? localMsgId)
    {
        var result = CreateBase(messageType, sourceNotice, originalMsgType, msgSvrId, localMsgId, rawKind, raw);

        result.Title = FirstNonEmpty(
            FindXmlValue(xml, "title"),
            FindXmlFirstValue(xml, "analysistitle", "pay_memo"),
            FindXmlAttribute(xml, "nickname"),
            FindXmlAttribute(xml, "username"));
        result.Description = FirstNonEmpty(
            FindXmlValue(xml, "des"),
            FindXmlValue(xml, "desc"),
            FindXmlFirstValue(xml, "feedbackdescription"),
            FindXmlValue(xml, "feedesc"),
            FindXmlValue(xml, "content"));
        result.Url = FirstNonEmpty(
            FindXmlValue(xml, "url"),
            FindXmlValue(xml, "dataurl"),
            FindXmlValue(xml, "nativeurl"),
            FindXmlAttribute(xml, "url"));
        result.ThumbUrl = FirstNonEmpty(
            FindXmlValue(xml, "thumburl"),
            FindXmlValue(xml, "thumb"),
            FindXmlValue(xml, "cdnthumburl"),
            FindXmlAttribute(xml, "thumburl"));
        result.IconUrl = FirstNonEmpty(
            FindXmlValue(xml, "iconurl"),
            FindXmlValue(xml, "icon"),
            FindXmlAttribute(xml, "headimgurl"));
        result.SourceName = FirstNonEmpty(
            FindXmlValue(xml, "sourcedisplayname"),
            FindXmlValue(xml, "sourceusername"),
            FindXmlValue(xml, "appname"));
        result.AppId = FirstNonEmpty(
            FindXmlValue(xml, "appid"),
            FindXmlAttribute(xml, "appid"));
        result.PagePath = FirstNonEmpty(
            FindXmlValue(xml, "pagepath"),
            FindXmlValue(xml, "weappinfo/pagepath"));
        result.AppMsgType = ParseInt(FindXmlValue(xml, "type"));
        result.TypeStr = FindXmlValue(xml, "typestr");
        result.Version = ParseInt(FindXmlValue(xml, "version"));
        result.Md5 = FirstNonEmpty(
            FindXmlValue(xml, "md5"),
            FindXmlAttribute(xml, "md5"));
        result.FileSize = FirstNonEmptyLong(
            ParseLong(FindXmlValue(xml, "totallen")),
            ParseLong(FindXmlValue(xml, "filesize")),
            ParseLong(FindXmlAttribute(xml, "length")));
        result.FileExtension = FirstNonEmpty(
            FindXmlValue(xml, "fileext"),
            FindXmlValue(xml, "filetype"));

        var hasLocation = HasXmlElement(xml, "location") || HasXmlElement(xml, "locationinfo");
        var hasQuote = HasXmlElement(xml, "refermsg") || HasXmlElement(xml, "quoteinfo");
        var hasFinder = HasAnyXmlElement(
                xml,
                "finderfeed",
                "finderlive",
                "feedid",
                "objectid",
                "finderobjectid",
                "liveid",
                "finderliveid",
                "nonceid",
                "objectnonceid",
                "findernonceid",
                "finderusername",
                "medialist")
            || (!string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "objectid", "finderobjectid"))
                && !string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "nonceid", "objectnonceid", "findernonceid")))
            || (!string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "finderusername"))
                && !string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "coverurl", "thumburl", "fullcoverurl", "headurl")));
        var hasPayment = messageType is MessageTypeLuckyMoney or MessageTypeMoneyTrans
            || HasXmlElement(xml, "wcpayinfo")
            || !string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "nativeurl", "transferid", "feedesc", "pay_memo", "paysubtype", "analysiskey"));
        var hasNameCard = messageType == MessageTypeNameCard
            || messageType == MessageTypeKefuNameCard
            || messageType == MessageTypeQiyeNameCard
            || !string.IsNullOrWhiteSpace(FindXmlAttribute(xml, "username"));
        var hasAppMsg = HasXmlElement(xml, "appmsg")
            || result.AppMsgType.HasValue
            || !string.IsNullOrWhiteSpace(result.Url)
            || !string.IsNullOrWhiteSpace(result.AppId)
            || !string.IsNullOrWhiteSpace(result.PagePath);

        if (hasLocation)
        {
            result.SemanticKind = "Location";
            result.Confidence = "High";
            result.Location = BuildLocation(xml, result);
            return result;
        }

        if (hasQuote)
        {
            result.SemanticKind = "Quote";
            result.Confidence = "High";
            result.Quote = BuildQuote(xml, result);
            return result;
        }

        if (hasPayment)
        {
            result.SemanticKind = messageType == MessageTypeLuckyMoney ? "LuckyMoney" : messageType == MessageTypeMoneyTrans ? "MoneyTrans" : "Payment";
            result.Confidence = "Medium";
            result.Payment = BuildPayment(xml, result);
            return result;
        }

        if (hasFinder)
        {
            result.SemanticKind = ResolveFinderKind(messageType, xml);
            result.Confidence = messageType == MessageTypeFinderFeed || messageType == MessageTypeFinderLive || messageType == MessageTypeRoomLiving ? "High" : "Medium";
            result.Finder = BuildFinder(xml, result);
            return result;
        }

        if (hasNameCard)
        {
            result.SemanticKind = "NameCard";
            result.Confidence = "High";
            result.NameCard = BuildNameCard(xml, result);
            return result;
        }

        if (hasAppMsg)
        {
            result.SemanticKind = ResolveAppMsgKind(messageType, result.AppMsgType, result.TypeStr, null);
            result.Confidence = "High";
            if (result.SemanticKind == "FinderFeed" || result.SemanticKind == "FinderLive" || result.SemanticKind == "RoomLiving")
            {
                result.Finder = BuildFinder(xml, result);
            }
            return result;
        }

        result.SemanticKind = ResolveKindFromMessageType(messageType);
        if (!IsUnknown(result.SemanticKind))
        {
            result.Confidence = "Low";
            return result;
        }

        return null;
    }

    private static AdvancedMessageContent? ParsePlainFallback(
        short messageType,
        string? content,
        string? sourceNotice,
        int? originalMsgType,
        long? msgSvrId,
        long? localMsgId)
    {
        var body = content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var kind = ResolveKindFromMessageType(messageType);
        if (IsUnknown(kind))
        {
            return null;
        }

        // 仅对服务端已经明确标记的高级类型做弱兜底，普通文本不生成扩展，避免污染 MessageExtensions。
        if (messageType is not (MessageTypeLink or MessageTypeLinkExt or MessageTypeFile or MessageTypeWeApp or MessageTypeNameCard or MessageTypeLocation or MessageTypeLuckyMoney or MessageTypeMoneyTrans or MessageTypeFinderFeed or MessageTypeFinderLive or MessageTypeRoomLiving or MessageTypeKefuNameCard or MessageTypeQiyeNameCard))
        {
            return null;
        }

        var result = CreateBase(messageType, sourceNotice, originalMsgType, msgSvrId, localMsgId, "Plain", body);
        result.SemanticKind = kind;
        result.Confidence = "Low";
        result.Title = Truncate(body, 120);
        if (LooksLikeUrl(body))
        {
            result.Url = body;
        }

        return result;
    }

    private static void FillCommonJsonFields(AdvancedMessageContent result, JsonElement root)
    {
        result.Title = GetString(root, "Title", "title", "Name", "name", "FileName", "fileName", "filename", "AnalysisTitle", "analysisTitle");
        result.Description = GetString(root, "Des", "des", "Desc", "desc", "Description", "description", "Content", "content", "Summary", "summary", "FeedbackDescription", "feedbackDescription", "FeeDescription", "feeDescription", "feedesc", "FeeDesc");
        result.Url = GetString(root, "Url", "url", "DataUrl", "dataUrl", "dataurl", "NativeUrl", "nativeurl", "WebUrl", "webUrl");
        result.ThumbUrl = GetString(root, "Thumb", "thumb", "ThumbUrl", "thumbUrl", "thumburl", "CdnThumbUrl", "cdnThumbUrl", "Cover", "cover", "CoverUrl", "coverUrl", "searchCachePath");
        result.IconUrl = GetString(root, "Icon", "icon", "IconUrl", "iconUrl", "HeadImg", "headImg", "HeadImage", "headImage", "userHeadImageUrl", "headImgUrl");
        result.SourceName = GetString(root, "SourceName", "sourceName", "SourceDisplayName", "sourceDisplayName", "AppName", "appName", "Nickname", "nickname");
        result.AppId = GetString(root, "AppId", "appId", "appid", "AppID", "appID");
        result.PagePath = GetString(root, "PagePath", "pagePath", "pagepath", "Path", "path");
        result.TypeStr = GetString(root, "TypeStr", "typeStr", "typestr", "TypeName", "typeName");
        result.AppMsgType = GetInt(root, "Type", "type", "AppMsgType", "appMsgType", "appmsgType");
        result.Version = GetInt(root, "Version", "version");
        result.DisableForward = GetBool(root, "DisableForward", "disableForward", "DisForward", "disForward");
        result.Md5 = GetString(root, "Md5", "md5", "MD5", "FileMd5", "fileMd5", "filemd5");
        result.FileSize = GetLong(root, "Size", "size", "FileSize", "fileSize", "Length", "length", "TotalLen", "totalLen", "TotalLength", "totalLength", "searchResultCount");
        result.FileExtension = GetString(root, "FileExtension", "fileExtension", "FileExt", "fileExt", "Ext", "ext", "Suffix", "suffix");
    }

    private static AdvancedLocationContent BuildLocation(JsonElement root, AdvancedMessageContent result)
    {
        var longitude = FirstNonEmptyDouble(
            GetDouble(root, "LocationX", "locationX", "longitude", "Longitude", "lng", "Lng", "x", "X"),
            null);
        var latitude = FirstNonEmptyDouble(
            GetDouble(root, "LocationY", "locationY", "latitude", "Latitude", "lat", "Lat", "y", "Y"),
            null);

        return new AdvancedLocationContent
        {
            Title = FirstNonEmpty(GetString(root, "Title", "title", "Poiname", "poiName", "PoiName"), result.Title),
            Label = GetString(root, "Label", "label", "Address", "address"),
            Longitude = longitude,
            Latitude = latitude,
            PoiId = GetString(root, "PoiId", "poiId", "POIId", "poiid"),
            Category = GetString(root, "Category", "category"),
            Phone = GetString(root, "Phone", "phone")
        };
    }

    private static AdvancedLocationContent BuildLocation(XDocument xml, AdvancedMessageContent result)
    {
        return new AdvancedLocationContent
        {
            Title = FirstNonEmpty(FindXmlAttribute(xml, "poiname"), FindXmlValue(xml, "poiname"), result.Title),
            Label = FirstNonEmpty(FindXmlAttribute(xml, "label"), FindXmlValue(xml, "label")),
            Longitude = FirstNonEmptyDouble(ParseDouble(FindXmlAttribute(xml, "x")), ParseDouble(FindXmlValue(xml, "x"))),
            Latitude = FirstNonEmptyDouble(ParseDouble(FindXmlAttribute(xml, "y")), ParseDouble(FindXmlValue(xml, "y"))),
            PoiId = FirstNonEmpty(FindXmlAttribute(xml, "poiid"), FindXmlValue(xml, "poiid")),
            Category = FirstNonEmpty(FindXmlAttribute(xml, "category"), FindXmlValue(xml, "category")),
            Phone = FirstNonEmpty(FindXmlAttribute(xml, "phone"), FindXmlValue(xml, "phone"))
        };
    }

    private static AdvancedQuoteContent BuildQuote(JsonElement root, AdvancedMessageContent result)
    {
        return new AdvancedQuoteContent
        {
            QuoteUser = GetString(root, "QuoteUser", "quoteUser", "chatusr", "chatUser"),
            DisplayName = GetString(root, "DisplayName", "displayName", "nickname", "Nickname"),
            QuoteType = GetInt(root, "QuoteType", "quoteType", "type", "Type"),
            QuoteSvrId = GetLong(root, "QuoteSvrId", "quoteSvrId", "svrid", "SvrId", "MsgSvrId", "msgSvrId"),
            Content = FirstNonEmpty(GetString(root, "Content", "content", "QuoteContent", "quoteContent"), result.Description),
            Title = FirstNonEmpty(GetString(root, "Title", "title"), result.Title)
        };
    }

    private static AdvancedQuoteContent BuildQuote(XDocument xml, AdvancedMessageContent result)
    {
        return new AdvancedQuoteContent
        {
            QuoteUser = FirstNonEmpty(FindXmlValue(xml, "chatusr"), FindXmlValue(xml, "quoteuser")),
            DisplayName = FirstNonEmpty(FindXmlValue(xml, "displayname"), FindXmlValue(xml, "nickname")),
            QuoteType = ParseInt(FirstNonEmpty(FindXmlValue(xml, "type"), FindXmlValue(xml, "quotetype"))),
            QuoteSvrId = ParseLong(FirstNonEmpty(FindXmlValue(xml, "svrid"), FindXmlValue(xml, "quotesvrid"))),
            Content = FirstNonEmpty(FindXmlValue(xml, "content"), result.Description),
            Title = result.Title
        };
    }

    private static AdvancedFinderContent BuildFinder(JsonElement root, AdvancedMessageContent result)
    {
        return new AdvancedFinderContent
        {
            FeedId = GetString(root, "FeedId", "feedId", "feedid", "Id", "id", "ObjectId", "objectId", "objectID", "FinderObjectID", "finderObjectID", "finderObjectId"),
            LiveId = GetString(root, "LiveId", "liveId", "liveid", "FinderLiveID", "finderLiveID", "FinderLiveId", "finderLiveId"),
            NonceId = GetString(root, "NonceId", "nonceId", "nonceid", "ObjectNonceId", "objectNonceId", "FinderNonceID", "finderNonceID", "FinderNonceId", "finderNonceId"),
            Username = GetString(root, "Username", "username", "finderUsername", "FinderUsername", "finderUserName", "FinderUserName"),
            Nickname = FirstNonEmpty(GetString(root, "Nickname", "nickname", "userNickname", "UserNickname"), result.SourceName),
            CoverUrl = FirstNonEmpty(GetString(root, "Cover", "cover", "CoverUrl", "coverUrl", "ThumbUrl", "thumbUrl", "FullCoverUrl", "fullCoverUrl", "HeadUrl", "headUrl"), result.ThumbUrl, result.IconUrl),
            Description = FirstNonEmpty(GetString(root, "Description", "description", "Desc", "desc", "Des", "des", "FinderDesc", "finderDesc"), result.Description)
        };
    }

    private static AdvancedFinderContent BuildFinder(XDocument xml, AdvancedMessageContent result)
    {
        return new AdvancedFinderContent
        {
            FeedId = FirstNonEmpty(
                FindXmlFirstValue(xml, "feedid", "objectid", "finderobjectid"),
                FindXmlFirstAttribute(xml, "feedid", "objectid", "finderobjectid")),
            LiveId = FirstNonEmpty(
                FindXmlFirstValue(xml, "liveid", "finderliveid"),
                FindXmlFirstAttribute(xml, "liveid", "finderliveid")),
            NonceId = FirstNonEmpty(
                FindXmlFirstValue(xml, "nonceid", "objectnonceid", "findernonceid"),
                FindXmlFirstAttribute(xml, "nonceid", "objectnonceid", "findernonceid")),
            Username = FirstNonEmpty(
                FindXmlFirstValue(xml, "finderusername", "username"),
                FindXmlFirstAttribute(xml, "finderusername", "username")),
            Nickname = FirstNonEmpty(
                FindXmlFirstValue(xml, "nickname", "usernickname"),
                FindXmlFirstAttribute(xml, "nickname", "usernickname"),
                result.SourceName),
            CoverUrl = FirstNonEmpty(
                FindXmlFirstValue(xml, "coverurl", "thumburl", "fullcoverurl", "headurl"),
                FindXmlFirstAttribute(xml, "coverurl", "thumburl", "fullcoverurl", "headurl"),
                result.ThumbUrl,
                result.IconUrl),
            Description = FirstNonEmpty(
                FindXmlFirstValue(xml, "desc", "description", "des"),
                result.Description)
        };
    }

    private static AdvancedNameCardContent BuildNameCard(JsonElement root, AdvancedMessageContent result)
    {
        return new AdvancedNameCardContent
        {
            Username = GetString(root, "Username", "username", "UserName", "userName", "Wxid", "wxid"),
            Nickname = FirstNonEmpty(GetString(root, "Nickname", "nickname", "NickName", "nickName", "userNickname"), result.Title, result.SourceName),
            HeadImgUrl = FirstNonEmpty(GetString(root, "HeadImg", "headImg", "HeadImgUrl", "headImgUrl", "userHeadImageUrl"), result.IconUrl, result.ThumbUrl),
            Alias = GetString(root, "Alias", "alias"),
            Province = GetString(root, "Province", "province"),
            City = GetString(root, "City", "city")
        };
    }

    private static AdvancedNameCardContent BuildNameCard(XDocument xml, AdvancedMessageContent result)
    {
        return new AdvancedNameCardContent
        {
            Username = FirstNonEmpty(FindXmlAttribute(xml, "username"), FindXmlValue(xml, "username")),
            Nickname = FirstNonEmpty(FindXmlAttribute(xml, "nickname"), FindXmlValue(xml, "nickname"), result.Title),
            HeadImgUrl = FirstNonEmpty(FindXmlAttribute(xml, "headimgurl"), FindXmlValue(xml, "headimgurl"), result.IconUrl),
            Alias = FirstNonEmpty(FindXmlAttribute(xml, "alias"), FindXmlValue(xml, "alias")),
            Province = FirstNonEmpty(FindXmlAttribute(xml, "province"), FindXmlValue(xml, "province")),
            City = FirstNonEmpty(FindXmlAttribute(xml, "city"), FindXmlValue(xml, "city"))
        };
    }

    private static AdvancedPaymentContent BuildPayment(JsonElement root, AdvancedMessageContent result)
    {
        var analysisKey = GetString(root, "AnalysisKey", "analysisKey");

        return new AdvancedPaymentContent
        {
            Key = FirstNonEmpty(GetString(root, "Key", "key"), analysisKey),
            NativeUrl = FirstNonEmpty(GetString(root, "NativeUrl", "nativeurl", "nativeUrl"), result.Url),
            FeeDescription = FirstNonEmpty(
                GetString(root, "FeeDescription", "feeDescription", "FeedbackDescription", "feedbackDescription", "feedesc", "FeeDesc", "AnalysisTitle", "analysisTitle"),
                result.Description,
                result.Title),
            TransferId = FirstNonEmpty(GetString(root, "TransferId", "transferId", "transferid"), analysisKey),
            HbType = GetString(root, "HbType", "hbType", "hbtype", "PaymentSubType", "paymentSubType", "PaySubType", "paysubtype"),
            TotalNum = GetInt(root, "TotalNum", "totalNum", "totalnum")
        };
    }

    private static AdvancedPaymentContent BuildPayment(XDocument xml, AdvancedMessageContent result)
    {
        var transferId = FindXmlValue(xml, "transferid");

        return new AdvancedPaymentContent
        {
            Key = FirstNonEmpty(FindXmlValue(xml, "key"), FindXmlValue(xml, "analysiskey"), transferId),
            NativeUrl = FirstNonEmpty(FindXmlValue(xml, "nativeurl"), result.Url),
            FeeDescription = FirstNonEmpty(
                FindXmlValue(xml, "feedbackdescription"),
                FindXmlValue(xml, "feedesc"),
                result.Description,
                FindXmlValue(xml, "pay_memo"),
                result.Title),
            TransferId = transferId,
            HbType = FirstNonEmpty(FindXmlValue(xml, "hbtype"), FindXmlValue(xml, "paysubtype")),
            TotalNum = ParseInt(FindXmlValue(xml, "totalnum"))
        };
    }

    private static bool LooksLikeQuoteJson(JsonElement root)
    {
        return HasAny(root, "QuoteSvrId", "quoteSvrId", "quoteSvrid", "QuoteType", "quoteType", "QuoteUser", "quoteUser")
            || (HasAny(root, "displayName", "DisplayName") && HasAny(root, "quoteType", "QuoteType", "quoteSvrId", "QuoteSvrId"));
    }

    private static bool LooksLikeLocationJson(JsonElement root)
    {
        return (HasAny(root, "LocationX", "locationX", "longitude", "Longitude", "lng", "Lng", "x", "X")
                && HasAny(root, "LocationY", "locationY", "latitude", "Latitude", "lat", "Lat", "y", "Y"))
            || HasAny(root, "PoiId", "poiId", "POIId");
    }

    private static bool LooksLikePaymentJson(JsonElement root)
    {
        return HasAny(
                root,
                "Key",
                "key",
                "nativeurl",
                "NativeUrl",
                "nativeUrl",
                "hbtype",
                "HbType",
                "hbType",
                "totalNum",
                "TotalNum",
                "totalnum",
                "transferid",
                "TransferId",
                "transferId",
                "analysisKey",
                "AnalysisKey",
                "feedesc",
                "FeeDesc",
                "feeDescription",
                "FeeDescription")
            || (HasAny(root, "analysisTitle", "AnalysisTitle", "feedbackDescription", "FeedbackDescription", "paymentSubType", "PaymentSubType", "paysubtype", "PaySubType")
                && HasAny(root, "feedbackDescription", "FeedbackDescription", "analysisKey", "AnalysisKey", "paymentSubType", "PaymentSubType", "paysubtype", "PaySubType", "payerInfo", "PayerInfo", "receiverInfo", "ReceiverInfo", "dataInvalidTime", "DataInvalidTime", "invalidTime", "InvalidTime"));
    }

    private static bool LooksLikeFinderJson(JsonElement root)
    {
        return HasAny(root, "FeedId", "feedId", "feedid", "LiveId", "liveId", "liveid", "FinderLiveID", "finderLiveID", "NonceId", "nonceId", "nonceid", "mediaList", "MediaList")
            || (HasAny(root, "Id", "id", "ObjectId", "objectId", "objectID", "FinderObjectID", "finderObjectID")
                && HasAny(root, "NonceId", "nonceId", "nonceid", "ObjectNonceId", "objectNonceId", "FinderNonceID", "finderNonceID", "mediaList", "MediaList"))
            || (HasAny(root, "finderUsername", "FinderUsername") && HasAny(root, "cover", "Cover", "coverUrl", "CoverUrl", "thumbUrl", "ThumbUrl", "fullCoverUrl", "FullCoverUrl", "headUrl", "HeadUrl"));
    }

    private static bool LooksLikeNameCardJson(JsonElement root, short messageType)
    {
        if (messageType == MessageTypeNameCard || messageType == MessageTypeKefuNameCard || messageType == MessageTypeQiyeNameCard)
        {
            return true;
        }

        return HasAny(root, "Username", "username", "UserName", "userName", "Wxid", "wxid")
            && HasAny(root, "Nickname", "nickname", "NickName", "nickName", "HeadImg", "headImg", "HeadImgUrl", "headImgUrl", "userHeadImageUrl");
    }

    private static bool LooksLikeAppMsgJson(JsonElement root, AdvancedMessageContent result)
    {
        var looksLikeCacheOnly = !string.IsNullOrWhiteSpace(result.Md5)
            && HasAny(root, "searchCachePath", "SearchCachePath");

        return result.AppMsgType.HasValue
            || HasAny(root, "TypeStr", "typeStr", "typestr", "AppId", "appId", "appid", "PagePath", "pagePath", "DataUrl", "dataUrl", "SourceName", "sourceName")
            || (!looksLikeCacheOnly && !string.IsNullOrWhiteSpace(result.Title) && (!string.IsNullOrWhiteSpace(result.Url) || !string.IsNullOrWhiteSpace(result.ThumbUrl)))
            || (!looksLikeCacheOnly && !string.IsNullOrWhiteSpace(result.Url) && HasAny(root, "Des", "des", "Desc", "desc", "Thumb", "thumb", "Icon", "icon"));
    }

    private static bool LooksLikeEmojiCacheJson(JsonElement root, AdvancedMessageContent result)
    {
        return !string.IsNullOrWhiteSpace(result.Md5)
            && (HasAny(root, "Thumb", "thumb", "ThumbUrl", "thumbUrl", "Size", "size", "searchCachePath", "searchResultCount")
                || !string.IsNullOrWhiteSpace(result.ThumbUrl)
                || result.FileSize.GetValueOrDefault() > 0);
    }

    private static bool LooksLikeFileCacheJson(JsonElement root, AdvancedMessageContent result)
    {
        return LooksLikeExplicitFileCacheJson(root, result)
            || (!string.IsNullOrWhiteSpace(result.Url) && (!string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.FileExtension)))
            || (!string.IsNullOrWhiteSpace(result.Md5) && HasAny(root, "FileSize", "fileSize", "TotalLen", "totalLen"))
            || (!string.IsNullOrWhiteSpace(result.Md5)
                && HasAny(root, "searchCachePath", "SearchCachePath")
                && (HasAny(root, "Title", "title", "FileName", "fileName", "filename", "FileExt", "fileExt", "FileExtension", "fileExtension", "Size", "size", "searchResultCount", "SearchResultCount")
                    || !string.IsNullOrWhiteSpace(result.Title)
                    || !string.IsNullOrWhiteSpace(result.FileExtension)));
    }

    private static bool LooksLikeExplicitFileCacheJson(JsonElement root, AdvancedMessageContent result)
    {
        return HasAny(root, "FileName", "fileName", "filename", "FileExt", "fileExt", "FileExtension", "fileExtension")
            || !string.IsNullOrWhiteSpace(result.FileExtension)
            || LooksLikeDocumentFilePath(result.Title)
            || LooksLikeDocumentFilePath(result.Url)
            || LooksLikeDocumentFilePath(result.ThumbUrl);
    }

    private static bool LooksLikeDocumentFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var queryIndex = text.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            text = text[..queryIndex];
        }

        var slashIndex = Math.Max(text.LastIndexOf('/'), text.LastIndexOf('\\'));
        var dotIndex = text.LastIndexOf('.');
        if (dotIndex <= slashIndex || dotIndex < 0 || dotIndex == text.Length - 1)
        {
            return false;
        }

        var ext = text[(dotIndex + 1)..].Trim().ToLowerInvariant();
        return ext is "pdf" or "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx"
            or "txt" or "csv" or "zip" or "rar" or "7z" or "apk";
    }

    private static string ResolveAppMsgKind(short messageType, int? appMsgType, string? typeStr, JsonElement? root)
    {
        var text = (typeStr ?? string.Empty).Trim();
        if (messageType == MessageTypeRoomLiving)
        {
            return "RoomLiving";
        }

        if (messageType == MessageTypeWeApp || appMsgType is 33 or 36 || ContainsAny(text, "小程序", "weapp", "mini program", "miniprogram"))
        {
            return "WeApp";
        }

        if (messageType == MessageTypeFile || appMsgType is 6 or 74 || ContainsAny(text, "文件", "file"))
        {
            return "File";
        }

        if (messageType == MessageTypeFinderLive || ContainsAny(text, "直播", "finder live") || (root.HasValue && HasAny(root.Value, "LiveId", "liveId", "liveid", "FinderLiveID", "finderLiveID")))
        {
            return "FinderLive";
        }

        if (messageType == MessageTypeFinderFeed
            || appMsgType == 51
            || ContainsAny(text, "视频号", "finder")
            || (root.HasValue && HasAny(root.Value, "FeedId", "feedId", "feedid", "FinderObjectID", "finderObjectID"))
            || (root.HasValue
                && HasAny(root.Value, "Id", "id", "ObjectId", "objectId", "objectID", "FinderObjectID", "finderObjectID")
                && HasAny(root.Value, "NonceId", "nonceId", "nonceid", "ObjectNonceId", "objectNonceId", "FinderNonceID", "finderNonceID", "mediaList", "MediaList")))
        {
            return "FinderFeed";
        }

        if (messageType == MessageTypeMoneyTrans)
        {
            return "MoneyTrans";
        }

        if (messageType == MessageTypeLuckyMoney)
        {
            return "LuckyMoney";
        }

        return "Link";
    }

    private static string ResolveFinderKind(short messageType, JsonElement root)
    {
        if (messageType == MessageTypeRoomLiving)
        {
            return "RoomLiving";
        }

        return messageType == MessageTypeFinderLive || HasAny(root, "LiveId", "liveId", "liveid", "FinderLiveID", "finderLiveID", "FinderLiveId", "finderLiveId")
            ? "FinderLive"
            : "FinderFeed";
    }

    private static string ResolveFinderKind(short messageType, XDocument xml)
    {
        if (messageType == MessageTypeRoomLiving)
        {
            return "RoomLiving";
        }

        return messageType == MessageTypeFinderLive
            || HasXmlElement(xml, "finderlive")
            || !string.IsNullOrWhiteSpace(FindXmlFirstValue(xml, "liveid", "finderliveid"))
            ? "FinderLive"
            : "FinderFeed";
    }

    private static string ResolveKindFromMessageType(short messageType)
    {
        return messageType switch
        {
            MessageTypeLink or MessageTypeLinkExt or MessageTypeBizLink => "Link",
            MessageTypeFile => "File",
            MessageTypeNameCard or MessageTypeKefuNameCard or MessageTypeQiyeNameCard => "NameCard",
            MessageTypeLocation => "Location",
            MessageTypeLuckyMoney => "LuckyMoney",
            MessageTypeMoneyTrans => "MoneyTrans",
            MessageTypeWeApp => "WeApp",
            MessageTypeEmoji => "Emoji",
            MessageTypeQuote => "Quote",
            MessageTypeFinderFeed => "FinderFeed",
            MessageTypeFinderLive => "FinderLive",
            MessageTypeRoomLiving => "RoomLiving",
            _ => "Unknown"
        };
    }

    private static AdvancedMessageContent CreateBase(
        short messageType,
        string? sourceNotice,
        int? originalMsgType,
        long? msgSvrId,
        long? localMsgId,
        string rawKind,
        string raw)
    {
        return new AdvancedMessageContent
        {
            SourceNotice = sourceNotice?.Trim() ?? string.Empty,
            SemanticKind = "Unknown",
            Confidence = "Low",
            MessageType = messageType,
            OriginalMsgType = originalMsgType,
            MsgSvrId = msgSvrId,
            LocalMsgId = localMsgId,
            RawKind = rawKind,
            RawPreview = Truncate(raw, 500),
            RawHash = ComputeSha256(raw),
            ParsedAt = DateTime.UtcNow
        };
    }

    private static bool TryParseJsonCandidate(string? text, out string jsonText, out JsonDocument document)
    {
        jsonText = string.Empty;
        document = null!;
        var candidate = ExtractObjectText(text, '{', '}');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(candidate);
            jsonText = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseXmlCandidate(string? text, out string xmlText, out XDocument document)
    {
        xmlText = string.Empty;
        document = null!;
        var candidate = ExtractObjectText(text, '<', '>');
        if (string.IsNullOrWhiteSpace(candidate) || !candidate.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            return false;
        }

        if (!LooksLikeXmlEnvelope(candidate))
        {
            return false;
        }

        try
        {
            document = XDocument.Parse(candidate, LoadOptions.PreserveWhitespace);
            xmlText = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 在进入 XDocument 前做轻量 XML 外壳检查，减少普通文本或残缺片段造成的一阶 XmlException 噪声。
    /// <para>这里只判断根节点外壳和常见未转义 &amp;，真正字段解析仍以 XDocument 为准。</para>
    /// </summary>
    private static bool LooksLikeXmlEnvelope(string candidate)
    {
        var text = candidate.Trim();
        if (text.Length < 3 || text[0] != '<')
        {
            return false;
        }

        if (ContainsLikelyUnescapedAmpersand(text))
        {
            return false;
        }

        var index = 0;
        while (index < text.Length)
        {
            SkipWhitespace(text, ref index);

            if (StartsWithAt(text, index, "<?"))
            {
                var end = text.IndexOf("?>", index, StringComparison.Ordinal);
                if (end < 0)
                {
                    return false;
                }

                index = end + 2;
                continue;
            }

            if (StartsWithAt(text, index, "<!--"))
            {
                var end = text.IndexOf("-->", index, StringComparison.Ordinal);
                if (end < 0)
                {
                    return false;
                }

                index = end + 3;
                continue;
            }

            break;
        }

        SkipWhitespace(text, ref index);
        if (index >= text.Length || text[index] != '<')
        {
            return false;
        }

        var nameStart = index + 1;
        if (nameStart >= text.Length || text[nameStart] is '/' or '!' or '?')
        {
            return false;
        }

        while (nameStart < text.Length && char.IsWhiteSpace(text[nameStart]))
        {
            nameStart++;
        }

        if (nameStart >= text.Length || !IsXmlNameStart(text[nameStart]))
        {
            return false;
        }

        var nameEnd = nameStart + 1;
        while (nameEnd < text.Length && IsXmlNameChar(text[nameEnd]))
        {
            nameEnd++;
        }

        var rootName = text[nameStart..nameEnd];
        var startTagEnd = FindTagEnd(text, index);
        if (startTagEnd < 0)
        {
            return false;
        }

        var beforeEnd = startTagEnd - 1;
        while (beforeEnd > index && char.IsWhiteSpace(text[beforeEnd]))
        {
            beforeEnd--;
        }

        if (beforeEnd > index && text[beforeEnd] == '/')
        {
            return true;
        }

        return text.EndsWith($"</{rootName}>", StringComparison.Ordinal);
    }

    private static bool ContainsLikelyUnescapedAmpersand(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                continue;
            }

            var semicolon = text.IndexOf(';', index + 1);
            if (semicolon < 0 || semicolon - index > 12)
            {
                return true;
            }

            var entity = text[(index + 1)..semicolon];
            if (entity is "amp" or "lt" or "gt" or "quot" or "apos")
            {
                index = semicolon;
                continue;
            }

            if (entity.StartsWith("#", StringComparison.Ordinal)
                && entity.Length > 1
                && entity.Skip(1).All(char.IsDigit))
            {
                index = semicolon;
                continue;
            }

            if (entity.StartsWith("#x", StringComparison.OrdinalIgnoreCase)
                && entity.Length > 2
                && entity.Skip(2).All(Uri.IsHexDigit))
            {
                index = semicolon;
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool StartsWithAt(string text, int index, string value)
    {
        return index >= 0
            && index + value.Length <= text.Length
            && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static int FindTagEnd(string text, int startIndex)
    {
        var quote = '\0';
        for (var index = startIndex; index < text.Length; index++)
        {
            var ch = text[index];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsXmlNameStart(char ch)
    {
        return char.IsLetter(ch) || ch is '_' or ':';
    }

    private static bool IsXmlNameChar(char ch)
    {
        return IsXmlNameStart(ch) || char.IsDigit(ch) || ch is '-' or '.';
    }

    private static string ExtractObjectText(string? text, char open, char close)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith(open) && trimmed.EndsWith(close))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf(open);
        var end = trimmed.LastIndexOf(close);
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)].Trim();
        }

        return string.Empty;
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement property, params string[] names)
    {
        property = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        foreach (var item in root.EnumerateObject())
        {
            if (names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                property = item.Value;
                return true;
            }
        }

        return false;
    }

    private static bool HasAny(JsonElement root, params string[] names)
    {
        return TryGetProperty(root, out _, names);
    }

    private static string GetString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static int? GetInt(JsonElement root, params string[] names)
    {
        var value = GetLong(root, names);
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value >= int.MinValue && value.Value <= int.MaxValue ? (int)value.Value : null;
    }

    private static long? GetLong(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numeric))
        {
            return numeric;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return ParseLong(property.GetString());
        }

        return null;
    }

    private static double? GetDouble(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return ParseDouble(property.GetString());
        }

        return null;
    }

    private static bool? GetBool(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool HasXmlElement(XDocument document, string localName)
    {
        return document.Descendants().Any(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnyXmlElement(XDocument document, params string[] localNames)
    {
        return localNames.Any(localName => HasXmlElement(document, localName));
    }

    private static string FindXmlValue(XDocument document, string localName)
    {
        if (string.IsNullOrWhiteSpace(localName))
        {
            return string.Empty;
        }

        var slashIndex = localName.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < localName.Length - 1)
        {
            localName = localName[(slashIndex + 1)..];
        }

        var element = document.Descendants()
            .FirstOrDefault(item => string.Equals(item.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        return element?.Value?.Trim() ?? string.Empty;
    }

    private static string FindXmlAttribute(XDocument document, string attributeName)
    {
        var attribute = document.Descendants()
            .SelectMany(item => item.Attributes())
            .FirstOrDefault(item => string.Equals(item.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase));
        return attribute?.Value?.Trim() ?? string.Empty;
    }

    private static string FindXmlFirstValue(XDocument document, params string[] localNames)
    {
        foreach (var localName in localNames)
        {
            var value = FindXmlValue(document, localName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string FindXmlFirstAttribute(XDocument document, params string[] attributeNames)
    {
        foreach (var attributeName in attributeNames)
        {
            var value = FindXmlAttribute(document, attributeName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static int? ParseInt(string? value)
    {
        var parsed = ParseLong(value);
        if (!parsed.HasValue)
        {
            return null;
        }

        return parsed.Value >= int.MinValue && parsed.Value <= int.MaxValue ? (int)parsed.Value : null;
    }

    private static long? ParseLong(string? value)
    {
        if (long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? ParseDouble(string? value)
    {
        if (double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? FirstNonEmptyLong(params long?[] values)
    {
        return values.FirstOrDefault(value => value.HasValue);
    }

    private static double? FirstNonEmptyDouble(params double?[] values)
    {
        return values.FirstOrDefault(value => value.HasValue);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return !string.IsNullOrWhiteSpace(text)
            && needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnknown(string? semanticKind)
    {
        return string.IsNullOrWhiteSpace(semanticKind)
            || string.Equals(semanticKind, "Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record CandidateText(string? Text, string Source);
}

/// <summary>
/// 高级聊天内容解析结果。
/// </summary>
public sealed class AdvancedMessageContent
{
    public int SchemaVersion { get; set; } = 1;
    public string SourceNotice { get; set; } = string.Empty;
    public string SemanticKind { get; set; } = "Unknown";
    public string Confidence { get; set; } = "Low";
    public short MessageType { get; set; }
    public int? OriginalMsgType { get; set; }
    public long? MsgSvrId { get; set; }
    public long? LocalMsgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbUrl { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string PagePath { get; set; } = string.Empty;
    public int? AppMsgType { get; set; }
    public string TypeStr { get; set; } = string.Empty;
    public int? Version { get; set; }
    public bool? DisableForward { get; set; }
    public string Md5 { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public AdvancedLocationContent? Location { get; set; }
    public AdvancedQuoteContent? Quote { get; set; }
    public AdvancedFinderContent? Finder { get; set; }
    public AdvancedNameCardContent? NameCard { get; set; }
    public AdvancedPaymentContent? Payment { get; set; }
    public string RawKind { get; set; } = string.Empty;
    public string RawPreview { get; set; } = string.Empty;
    public string RawHash { get; set; } = string.Empty;
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AdvancedLocationContent
{
    public string Title { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string PoiId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public sealed class AdvancedQuoteContent
{
    public string QuoteUser { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? QuoteType { get; set; }
    public long? QuoteSvrId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public sealed class AdvancedFinderContent
{
    public string FeedId { get; set; } = string.Empty;
    public string LiveId { get; set; } = string.Empty;
    public string NonceId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class AdvancedNameCardContent
{
    public string Username { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string HeadImgUrl { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public sealed class AdvancedPaymentContent
{
    public string Key { get; set; } = string.Empty;
    public string NativeUrl { get; set; } = string.Empty;
    public string FeeDescription { get; set; } = string.Empty;
    public string TransferId { get; set; } = string.Empty;
    public string HbType { get; set; } = string.Empty;
    public int? TotalNum { get; set; }
}
