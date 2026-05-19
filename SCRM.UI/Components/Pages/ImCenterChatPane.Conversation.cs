using System.Text.RegularExpressions;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 会话、成员与消息归属显示

    private static readonly Regex GroupMemberPrefixRegex = new(
        @"^(?<wxid>(?:wxid_[A-Za-z0-9_\-]{6,}|gh_[A-Za-z0-9_\-]{3,}|[A-Za-z0-9_\-.]+@(?:chatroom|openim)))(?:\s*[:：]\s*|\s+|(?=[\u4e00-\u9fff\p{P}\p{S}]))(?<body>[\s\S]*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private string ContextAvatar()
    {
        if (Store.SelectedContact != null) return Store.SelectedContact.avatar ?? "images/default-avatar.png";
        if (Store.SelectedConversation != null) return Store.SelectedConversation.displayAvatar ?? "images/default-avatar.png";
        return "images/default-avatar.png";
    }

    private bool IsMyMessage(Message msg)
    {
        var myWxid = Store.SelectedDevice?.wx?.wechatAccount?.wxid;
        
        // 优先根据实际 WXID 判断真理
        if (!string.IsNullOrEmpty(myWxid) && !string.IsNullOrEmpty(msg.senderWxid))
        {
            if (msg.senderWxid == myWxid) return true;
        }
        
        // 兜底降级：依靠由于某些原因缺失 senderWxid 但是设定了 direction 的情况
        if (msg.direction == 1) return true;

        // 以上皆不满足，则判定为对方消息
        return false;
    }

    /// <summary>
    /// 拆分群聊消息前缀。
    /// <para>62203 群聊上报常见格式是“成员wxid:正文”；也可能因为日志/转码变成“成员wxid正文”。这里只做展示层拆分，不改数据库原文。</para>
    /// </summary>
    private GroupMessagePrefix ParseGroupMemberPrefix(Message msg)
    {
        var content = msg.content ?? string.Empty;
        if (!IsCurrentConversationChatRoom() || string.IsNullOrWhiteSpace(content))
        {
            return new GroupMessagePrefix(string.Empty, content);
        }

        var trimmed = content.TrimStart();
        if (TryParseKnownGroupMemberPrefix(trimmed, out var knownPrefix))
        {
            return knownPrefix;
        }

        var match = GroupMemberPrefixRegex.Match(trimmed);
        if (!match.Success)
        {
            return new GroupMessagePrefix(string.Empty, content);
        }

        var candidate = match.Groups["wxid"].Value.Trim();
        if (!LooksLikeWxid(candidate) || IsChatRoomWxid(candidate))
        {
            return new GroupMessagePrefix(string.Empty, content);
        }

        var body = match.Groups["body"].Value.TrimStart('\r', '\n', ':', '：', ' ', '\t');
        return new GroupMessagePrefix(candidate, body);
    }

    /// <summary>
    /// 用已知联系人/群成员 wxid 拆分无分隔符群消息。
    /// <para>例如“wxid_xxx312”在正则里无法知道 wxid 到哪一位结束；如果当前缓存已有 wxid，则优先按精确前缀拆分。</para>
    /// </summary>
    private bool TryParseKnownGroupMemberPrefix(string content, out GroupMessagePrefix prefix)
    {
        prefix = new GroupMessagePrefix(string.Empty, content);
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        foreach (var wxid in EnumerateKnownParticipantWxids().OrderByDescending(id => id.Length))
        {
            if (!content.StartsWith(wxid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var body = content[wxid.Length..].TrimStart(':', '：', '\r', '\n', ' ', '\t');
            prefix = new GroupMessagePrefix(wxid, body);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 单聊展示层兜底：剥离旧版安卓上报时误拼到正文前的 friendId / senderWxid。
    /// <para>这里只影响 UI 展示，不改数据库；群聊仍由 ParseGroupMemberPrefix 保留成员前缀解析。</para>
    /// </summary>
    private string NormalizePrivateChatBody(Message msg, string body, bool isMine)
    {
        if (IsCurrentConversationChatRoom() || string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        var normalized = body;
        foreach (var prefix in EnumeratePrivateChatBodyPrefixes(msg)
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => p.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(p => p.Length))
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stripped = StripPrivateBodyPrefixForDisplay(normalized, prefix, forceKnownPrefix: true);
            if (!string.Equals(stripped, normalized, StringComparison.Ordinal))
            {
                normalized = stripped;
                break;
            }
        }

        normalized = StripLooseWxidPrefixForDisplay(normalized);
        return TrimAccidentalCoordinateSuffix(normalized);
    }

    /// <summary>
    /// 单聊展示层剥离误拼接的 wxid 前缀。
    /// <para>服务端持久化不能凭“wxid_xxx12”盲删，因为 wxid 本身可能以数字继续；UI 层有联系人表，可用最长已知 wxid 精确拆分。</para>
    /// </summary>
    private string StripPrivateBodyPrefixForDisplay(string body, string token, bool forceKnownPrefix = false)
    {
        if (string.IsNullOrEmpty(body) || string.IsNullOrWhiteSpace(token))
        {
            return body;
        }

        var isKnownPrefix = forceKnownPrefix || IsKnownPrivateParticipantWxid(token);
        while (body.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = body[token.Length..];
            var cleanedRemainder = remainder.TrimStart(':', '：', (char)13, (char)10, ' ', (char)9);
            if (string.Equals(cleanedRemainder, remainder, StringComparison.Ordinal)
                && LooksLikeWxidContinuation(token, cleanedRemainder)
                && !isKnownPrefix)
            {
                break;
            }

            body = cleanedRemainder;
        }

        return body;
    }

    private bool IsKnownPrivateParticipantWxid(string wxid)
    {
        if (string.IsNullOrWhiteSpace(wxid))
        {
            return false;
        }

        return string.Equals(Store.SelectedConversation?.conversationWxid, wxid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Store.SelectedContact?.wxid, wxid, StringComparison.OrdinalIgnoreCase)
            || Store.CurrentMessages.Any(m =>
                string.Equals(m.senderWxid, wxid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.receiverWxid, wxid, StringComparison.OrdinalIgnoreCase))
            || FindContact(wxid) != null;
    }

    /// <summary>
    /// 单聊展示层兜底剥离疑似 wxid 前缀。
    /// <para>当服务端历史数据缺少完整 friendId 或 wxid 长度被拼接文本干扰时，前面的精确前缀匹配可能失效；这里只在单聊 UI 做保守清洗。</para>
    /// </summary>
    private static string StripLooseWxidPrefixForDisplay(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        // 常见 wxid 长度约 19~22 位。若后面紧跟中文或标点，大概率是“wxid + 正文”粘连。
        var match = Regex.Match(body, @"^(?<wxid>wxid_[A-Za-z0-9_\-]{14,22})(?<rest>[\s\S]*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return body;
        }

        var rest = match.Groups["rest"].Value;
        if (string.IsNullOrWhiteSpace(rest))
        {
            return body;
        }

        var first = rest[0];
        if (char.IsWhiteSpace(first) || first == ':' || first == '：' || IsLikelyMessageStartChar(first))
        {
            return rest.TrimStart(':', '：', (char)13, (char)10, ' ', (char)9);
        }

        return body;
    }

    /// <summary>
    /// 判断字符是否像正文开头，避免把真实 wxid 截断。
    /// </summary>
    private static bool IsLikelyMessageStartChar(char ch)
    {
        return (ch >= '\u4e00' && ch <= '\u9fff')
            || char.IsPunctuation(ch)
            || char.IsSymbol(ch);
    }

    private static bool LooksLikeWxidContinuation(string prefix, string remainder)
    {
        return prefix.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(remainder)
            && char.IsLetterOrDigit(remainder[0]);
    }

    /// <summary>
    /// 剥离单聊文本尾部误拼接的坐标片段。
    /// <para>部分 62203 实时消息会把点击坐标如“720.640”拼到正文尾部；这里只在展示层处理，不改数据库。</para>
    /// </summary>
    private static string TrimAccidentalCoordinateSuffix(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        return Regex.Replace(body, @"\s+\d{2,4}\.\d{2,4}\s*$", string.Empty, RegexOptions.CultureInvariant);
    }

    private IEnumerable<string> EnumeratePrivateChatBodyPrefixes(Message msg)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in new[]
        {
            msg.senderWxid,
            msg.receiverWxid,
            Store.SelectedConversation?.conversationWxid,
            Store.SelectedContact?.wxid,
            Store.SelectedContact?.friendNo
        })
        {
            if (!string.IsNullOrWhiteSpace(value) && !IsChatRoomWxid(value) && seen.Add(value.Trim()))
            {
                yield return value.Trim();
            }
        }

        foreach (var contact in Store.Contacts.Concat(Store.SelectedDevice?.wx?.contacts ?? Enumerable.Empty<Contact>()))
        {
            if (!string.IsNullOrWhiteSpace(contact.wxid) && seen.Add(contact.wxid.Trim()))
            {
                yield return contact.wxid.Trim();
            }

            if (!string.IsNullOrWhiteSpace(contact.friendNo) && seen.Add(contact.friendNo.Trim()))
            {
                yield return contact.friendNo.Trim();
            }
        }
    }

    /// <summary>
    /// 汇总当前页面已知的群成员/联系人 wxid。
    /// <para>优先用于无分隔符群消息前缀解析，不参与持久化。</para>
    /// </summary>
    private IEnumerable<string> EnumerateKnownParticipantWxids()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in Store.CurrentGroupMembers)
        {
            if (!string.IsNullOrWhiteSpace(member.memberWxid) && seen.Add(member.memberWxid.Trim()))
            {
                yield return member.memberWxid.Trim();
            }
        }

        foreach (var contact in Store.Contacts.Concat(Store.SelectedDevice?.wx?.contacts ?? Enumerable.Empty<Contact>()))
        {
            if (!string.IsNullOrWhiteSpace(contact.wxid) && seen.Add(contact.wxid.Trim()))
            {
                yield return contact.wxid.Trim();
            }
        }
    }

    private string ResolveMyDisplayName()
    {
        var account = Store.SelectedDevice?.wx?.wechatAccount;
        return FirstNonEmpty(account?.nickname, account?.wechatNumber, account?.wxid, Store.SelectedDevice?.weChatId, "我");
    }

    private string ResolveMemberDisplayName(string? memberWxid, string? fallbackWxid)
    {
        var wxid = FirstNonEmpty(memberWxid, fallbackWxid);
        if (IsCurrentConversationChatRoom())
        {
            if (string.IsNullOrWhiteSpace(wxid) || wxid.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
            {
                return Store.SelectedConversation?.displayName ?? "对方";
            }
        }
        else if (string.IsNullOrWhiteSpace(wxid)
            || string.Equals(wxid, Store.SelectedConversation?.conversationWxid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(wxid, Store.SelectedContact?.wxid, StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePrivateConversationDisplayName();
        }

        if (IsCurrentConversationChatRoom())
        {
            // 群聊成员展示优先用群名片/群成员昵称；成员不是好友时 Contacts 通常查不到。
            var member = FindCurrentGroupMember(wxid);
            if (member != null)
            {
                return FirstNonEmpty(member.memberRemarks, member.alias, member.memberNickname, member.memberWxid);
            }
        }

        var contact = FindContact(wxid);
        if (contact != null)
        {
            return FirstNonEmpty(contact.remarks, contact.nickname, contact.friendNo, contact.wxid);
        }

        return wxid;
    }

    private string ResolvePrivateConversationDisplayName()
    {
        var contact = FindContact(Store.SelectedConversation?.conversationWxid)
            ?? Store.SelectedContact;
        if (contact != null)
        {
            return FirstNonEmpty(contact.remarks, contact.nickname, contact.friendNo, contact.wxid);
        }

        return FirstNonEmpty(Store.SelectedConversation?.displayName, Store.SelectedConversation?.conversationWxid, "对方");
    }

    private string ResolveMemberAvatar(string? memberWxid, string? fallbackWxid)
    {
        var wxid = FirstNonEmpty(memberWxid, fallbackWxid);
        var member = FindCurrentGroupMember(wxid);
        if (member != null && !string.IsNullOrWhiteSpace(member.memberAvatar))
        {
            return member.memberAvatar;
        }

        var contact = FindContact(wxid);
        if (contact != null && !string.IsNullOrWhiteSpace(contact.avatar))
        {
            return contact.avatar;
        }

        return ContextAvatar();
    }

    private GroupMemberDto? FindCurrentGroupMember(string? wxid)
    {
        if (string.IsNullOrWhiteSpace(wxid))
        {
            return null;
        }

        return Store.CurrentGroupMembers.FirstOrDefault(m => string.Equals(m.memberWxid, wxid, StringComparison.OrdinalIgnoreCase));
    }

    private Contact? FindContact(string? wxid)
    {
        if (string.IsNullOrWhiteSpace(wxid))
        {
            return null;
        }

        return Store.Contacts.FirstOrDefault(c => string.Equals(c.wxid, wxid, StringComparison.OrdinalIgnoreCase))
            ?? Store.SelectedDevice?.wx?.contacts?.FirstOrDefault(c => string.Equals(c.wxid, wxid, StringComparison.OrdinalIgnoreCase))
            ?? Store.Contacts.FirstOrDefault(c => string.Equals(c.friendNo, wxid, StringComparison.OrdinalIgnoreCase))
            ?? Store.SelectedDevice?.wx?.contacts?.FirstOrDefault(c => string.Equals(c.friendNo, wxid, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsChatRoomWxid(string? wxid)
    {
        return !string.IsNullOrWhiteSpace(wxid)
            && (wxid.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase)
                || wxid.EndsWith("@openim", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeWxid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("gh_", StringComparison.OrdinalIgnoreCase)
            || IsChatRoomWxid(value)
            || value.Contains('@', StringComparison.Ordinal);
    }

    private bool IsCurrentConversationChatRoom()
    {
        var wxid = Store.SelectedConversation?.conversationWxid
            ?? Store.SelectedContact?.wxid
            ?? string.Empty;
        return IsChatRoomWxid(wxid)
            || Store.SelectedConversation?.conversationType == 2;
    }

    /// <summary>
    /// 从消息收发双方推断对端 wxid。
    /// </summary>
    private string ResolvePeerWxidFromMessage(Message msg)
    {
        var ownerWxid = Store.SelectedDevice?.wx?.wechatAccount?.wxid ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(msg.senderWxid)
            && !string.Equals(msg.senderWxid, ownerWxid, StringComparison.OrdinalIgnoreCase))
        {
            return msg.senderWxid;
        }

        if (!string.IsNullOrWhiteSpace(msg.receiverWxid)
            && !string.Equals(msg.receiverWxid, ownerWxid, StringComparison.OrdinalIgnoreCase))
        {
            return msg.receiverWxid;
        }

        return FirstNonEmpty(Store.SelectedConversation?.conversationWxid, Store.SelectedContact?.wxid);
    }

    #endregion
}