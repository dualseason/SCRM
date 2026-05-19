using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.Tests;

/// <summary>
/// CrmService 直连 UI 读取入口归属校验与脱敏防回归测试。
/// <para>Blazor Server 页面会直接注入 ICrmService，不能绕过 ClientHub/REST 已建立的账号边界和敏感字段出口脱敏。</para>
/// </summary>
public sealed class CrmServiceSecurityTests
{
    [Fact]
    public void CrmService_ShouldInjectCurrentUserGuardAndMaskingService()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        Assert.Contains("using Microsoft.AspNetCore.Components.Authorization;", source);
        Assert.Contains("using System.Security.Claims;", source);
        Assert.Contains("using SCRM.API.Services.Security;", source);

        Assert.Contains("private readonly AuthenticationStateProvider _authenticationStateProvider;", source);
        Assert.Contains("private readonly AccountAccessGuard _accountAccessGuard;", source);
        Assert.Contains("private readonly SensitiveMaskingService _sensitiveMaskingService;", source);
        Assert.Contains("private readonly ILogger<CrmService> _logger;", source);

        Assert.Contains("AuthenticationStateProvider authenticationStateProvider", source);
        Assert.Contains("AccountAccessGuard accountAccessGuard", source);
        Assert.Contains("SensitiveMaskingService sensitiveMaskingService", source);
        Assert.Contains("ILogger<CrmService> logger", source);

        var helper = ExtractMethod(source, "GetCurrentUserAsync");
        Assert.Contains("GetAuthenticationStateAsync()", helper);
        Assert.Contains("ClaimsPrincipal?", helper);
        Assert.Contains("按无权限处理", helper);
        Assert.Contains("return null", helper);
    }

    [Fact]
    public void CrmService_ReadMethods_ShouldGuardAndMaskHighRiskData()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        var contacts = ExtractMethod(source, "GetContactsAsync");
        Assert.Contains("var user = await GetCurrentUserAsync();", contacts);
        Assert.Contains("GetAccessibleAccountIdsAsync(user)", contacts);
        Assert.Contains("accessibleAccountIds.Contains(c.ownerWxid)", contacts);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", contacts);
        Assert.Contains("MaskContactsAsync(user, contacts)", contacts);
        Assert.Contains("MaskContactsAsync(user, accountContacts)", contacts);

        var conversations = ExtractMethod(source, "GetConversationsAsync");
        Assert.Contains("var user = await GetCurrentUserAsync();", conversations);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", conversations);
        Assert.Contains("MaskConversationsAsync(user, conversations)", conversations);

        var messages = ExtractMethod(source, "GetMessagesAsync");
        Assert.Contains("count = Math.Clamp(count, 1, 500);", messages);
        Assert.Contains("var user = await GetCurrentUserAsync();", messages);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", messages);
        Assert.Contains("EnrichMessagesWithMediaMetadataAsync(messages)", messages);
        Assert.Contains("MaskMessagesAsync(user, messages)", messages);

        var sms = ExtractMethod(source, "GetSmsRecordsAsync");
        Assert.Contains("count = Math.Clamp(count, 1, 500);", sms);
        Assert.Contains("var user = await GetCurrentUserAsync();", sms);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", sms);
        Assert.Contains("BuildProfileAsync(user)", sms);
        Assert.Contains("LogSensitiveFieldsReturnedAsync", sms);
        Assert.Contains("SensitiveMaskingService.MaskSmsRecord(record, profile)", sms);

        var calls = ExtractMethod(source, "GetCallLogRecordsAsync");
        Assert.Contains("count = Math.Clamp(count, 1, 500);", calls);
        Assert.Contains("var user = await GetCurrentUserAsync();", calls);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", calls);
        Assert.Contains("BuildProfileAsync(user)", calls);
        Assert.Contains("LogSensitiveFieldsReturnedAsync", calls);
        Assert.Contains("SensitiveMaskingService.MaskCallLogRecord(record, profile)", calls);
    }

    [Fact]
    public void CrmService_ExtendedReadMethods_ShouldGuardAndMaskCustomerData()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        var labels = ExtractMethod(source, "GetContactLabelsAsync");
        Assert.Contains("CanAccessAccountAsync(user, accountId)", labels);

        var groupMembers = ExtractMethod(source, "GetGroupMembersAsync");
        Assert.Contains("CanAccessAccountAsync(user, accountId)", groupMembers);
        Assert.Contains("MaskGroupMembersAsync(user, members)", groupMembers);

        var invitations = ExtractMethod(source, "GetGroupInvitationsAsync");
        Assert.Contains("count = Math.Clamp(count, 1, 200);", invitations);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", invitations);
        Assert.Contains("MaskGroupInvitationsAsync(user, invitations)", invitations);

        var massHistory = ExtractMethod(source, "GetMassSendHistoryAsync");
        Assert.Contains("Math.Clamp(count, 1, 200)", massHistory);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", massHistory);
        Assert.Contains("MaskMassSendHistoryAsync(user, result)", massHistory);

        var friendRequests = ExtractMethod(source, "GetFriendRequestsAsync");
        Assert.Contains("Math.Clamp(count, 1, 200)", friendRequests);
        Assert.Contains("CanAccessAccountAsync(user, accountId)", friendRequests);
        Assert.Contains("MaskFriendRequestsAsync(user, result)", friendRequests);

        var moments = ExtractMethod(source, "GetMomentsAsync");
        Assert.Contains("CanAccessDeviceAsync(user, deviceUuid)", moments);
        Assert.Contains("CanAccessAccountAsync(user, normalizedWeChatId)", moments);
        Assert.Contains("CanAccessAccountAsync(user, account.wxid)", moments);
        Assert.Contains("count = Math.Clamp(count, 1, 200);", moments);
        Assert.Contains("MaskMomentsAsync(user, moments)", moments);
    }

    [Fact]
    public void CrmService_DeviceCompanionMethods_ShouldGuardDeviceScope()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        foreach (var methodName in new[]
        {
            "SyncMassSendHistoryAsync",
            "PullFriendAddReqListAsync",
            "GetChatRoomInviteListAsync"
        })
        {
            var method = ExtractMethod(source, methodName);
            Assert.Contains("CanAccessDeviceAsync(user, deviceUuid)", method);
        }

        var finder = ExtractMethod(source, "GetFinderHistoryAsync<TDto>");
        Assert.Contains("CanAccessDeviceAsync(user, deviceUuid)", finder);
        Assert.Contains("CanAccessAccountAsync(user, account.wxid)", finder);
    }

    [Fact]
    public void SensitiveMaskingService_ShouldMaskExtendedCustomerDtos()
    {
        var profile = SensitiveAccessProfile.NoAccess;

        var groupMember = SensitiveMaskingService.MaskGroupMember(new GroupMemberDto
        {
            memberWxid = "wxid_member_abcdef",
            memberNickname = "群友 13812345678",
            alias = "名片 13812345678",
            memberRemarks = "备注 13812345678"
        }, profile);
        Assert.Equal("wxid_member_abcdef", groupMember.memberWxid);
        Assert.Equal("群友 138****5678", groupMember.memberNickname);
        Assert.Equal(string.Empty, groupMember.alias);
        Assert.Equal(string.Empty, groupMember.memberRemarks);

        var request = SensitiveMaskingService.MaskFriendRequest(new FriendRequestDto
        {
            ownerWxid = "wx-owner",
            requestWxid = "wxid_request_abcdef",
            nickname = "申请人 13812345678",
            source = "手机号 13812345678",
            requestMessage = "加我 13812345678"
        }, profile);
        Assert.Equal("wxid_request_abcdef", request.requestWxid);
        Assert.Equal("申请人 138****5678", request.nickname);
        Assert.Equal("[已隐藏]", request.source);
        Assert.Equal("[已隐藏]", request.requestMessage);

        var invitation = SensitiveMaskingService.MaskGroupInvitation(new GroupInvitationDto
        {
            weChatId = "wx-owner",
            chatRoomId = "room@chatroom",
            inviter = "wxid_inviter_abcdef",
            reason = "邀请 13812345678",
            invited = new List<GroupInvitationMemberDto>
            {
                new() { userName = "wxid_guest_abcdef", nickName = "客人 13812345678" }
            }
        }, profile);
        Assert.Equal("wxid_****cdef", invitation.inviter);
        Assert.Equal("[已隐藏]", invitation.reason);
        Assert.Equal("wxid_****cdef", invitation.invited[0].userName);

        var mass = SensitiveMaskingService.MaskMassSendHistory(new MassSendHistoryDto
        {
            ownerWxid = "wx-owner",
            messageTitle = "标题 13812345678",
            messageContent = "内容 13812345678",
            details = new List<MassSendHistoryDetailDto>
            {
                new() { recipientWxid = "wxid_receiver_abcdef", recipientDisplayName = "客户 13812345678" }
            }
        }, profile);
        Assert.Equal("[已隐藏]", mass.messageTitle);
        Assert.Equal("[消息内容已隐藏]", mass.messageContent);
        Assert.Equal("wxid_****cdef", mass.details[0].recipientWxid);
        Assert.Equal("客户 138****5678", mass.details[0].recipientDisplayName);

        var moment = SensitiveMaskingService.MaskMoment(new MomentsTimeline
        {
            userName = "wxid_author_abcdef",
            nickName = "作者 13812345678",
            content = "朋友圈 13812345678",
            commentsJson = "[{\"content\":\"13812345678\"}]",
            xmlContent = "<xml>13812345678</xml>",
            videoUrl = "https://file.local/a.mp4"
        }, profile);
        Assert.Equal("wxid_****cdef", moment.userName);
        Assert.Equal("作者 138****5678", moment.nickName);
        Assert.Equal("[消息内容已隐藏]", moment.content);
        Assert.Equal(string.Empty, moment.commentsJson);
        Assert.Equal(string.Empty, moment.xmlContent);
        Assert.Equal(string.Empty, moment.videoUrl);
    }

    [Fact]
    public void Program_ShouldKeepSecurityServicesRegisteredForCrmService()
    {
        var source = ReadSource("SCRM.API", "Program.cs");

        Assert.Contains("AddScoped<ICrmService, CrmService>()", source);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.AccountAccessGuard>()", source);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.SensitiveMaskingService>()", source);
        Assert.Contains("AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>()", source);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = Math.Max(
            source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal),
            source.LastIndexOf("private async Task", methodNameIndex, StringComparison.Ordinal));

        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

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

        throw new InvalidOperationException($"方法体未闭合：{methodName}");
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
