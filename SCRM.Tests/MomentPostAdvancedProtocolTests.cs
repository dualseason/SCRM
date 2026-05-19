namespace SCRM.Tests;

/// <summary>
/// 朋友圈 PostSNSNewsTask 高级协议防回归测试。
/// <para>确保 SCRM 不再只支持 content + imageUrls，而是能构造 62203/SmRun 已具备的高级发圈字段。</para>
/// </summary>
public sealed class MomentPostAdvancedProtocolTests
{
    [Fact]
    public void DtoAndPermissions_ShouldExpose62203MomentPostFields()
    {
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "MomentPostRequestDto.cs");
        var proto = ReadSource("SCRM.SHARED", "proto", "PostSNSNewsTask.proto");
        var permissions = ReadSource("SCRM.API", "Models", "Constants", "Permissions.cs");
        var seed = ReadSource("SCRM.API", "Data", "SeedData.cs");

        foreach (var field in new[]
        {
            "public sealed class MomentPostRequestDto",
            "weChatId",
            "content",
            "attachment",
            "comment",
            "sendSlow",
            "visible",
            "poi",
            "extComment",
            "notiUsers",
            "MomentPostAttachmentType",
            "MomentPostVisibleType",
            "MomentPostPoiDto",
            "FromLegacy"
        })
        {
            Assert.Contains(field, dto);
        }

        foreach (var attachType in new[] { "Link = 0", "Picture = 2", "ShortVideo = 3", "LongVideo = 4", "ShiPinHao = 5", "ExtLink = 6", "FinderLive = 7" })
        {
            Assert.Contains(attachType, dto);
        }

        foreach (var protoField in new[] { "WeChatId = 1", "Attachment = 3", "Comment = 4", "Visible = 6", "SendSlow = 7", "Poi = 8", "ExtComment = 9", "NotiUsers = 10" })
        {
            Assert.Contains(protoField, proto);
        }

        Assert.Contains("public const string Post = \"moment.post\"", permissions);
        Assert.Contains("Permissions.MomentOperation.Post", seed);
    }

    [Fact]
    public void ClientTaskService_ShouldConstructAdvancedPostSNSNewsProto()
    {
        var clientTask = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");

        var legacy = ExtractMethod(clientTask, "SendPostSNSNewsTaskAsync", "SendPostSNSNewsTaskAsync(string connectionId, string content");
        Assert.Contains("MomentPostRequestDto.FromLegacy(content, attachments)", legacy);

        var advanced = ExtractMethod(clientTask, "SendPostSNSNewsTaskAsync", "SendPostSNSNewsTaskAsync(string connectionId, MomentPostRequestDto? request");
        foreach (var required in new[]
        {
            "NormalizeMomentPostRequest(request)",
            "WeChatId = normalized.weChatId",
            "Content = normalized.content",
            "Comment = normalized.comment",
            "SendSlow = normalized.sendSlow",
            "MapMomentAttachmentType(normalized.attachment.type)",
            "task.Attachment.Content.Add(attachment)",
            "MapMomentVisibleType(normalized.visible.type)",
            "Labels = string.Join(\",\", normalized.visible.labels)",
            "Friends = string.Join(\",\", normalized.visible.friends)",
            "task.Poi = new PostSNSNewsTaskMessage.Types.PoiMessage",
            "task.ExtComment.AddRange(normalized.extComment)",
            "task.NotiUsers.AddRange(normalized.notiUsers)",
            "EnumMsgType.PostSnsnewsTask"
        })
        {
            Assert.Contains(required, advanced);
        }

        foreach (var helper in new[]
        {
            "MapMomentAttachmentType",
            "MapMomentVisibleType",
            "ShouldSendVisible",
            "ShouldSendPoi",
            "NormalizeMomentPostRequest"
        })
        {
            Assert.Contains(helper, clientTask);
        }
    }

    [Fact]
    public void CrmServiceHubStoreAndUi_ShouldUseAdvancedMomentPostPath()
    {
        var crmInterface = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var deviceCommand = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var validator = ReadSource("SCRM.API", "Services", "Core", "MomentPostRequestValidator.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");
        var wechatService = ReadSource("SCRM.UI", "Services", "WeChatService.cs");
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var controller = ReadSource("SCRM.API", "Controllers", "ClientTaskController.cs");

        Assert.Contains("PostMomentAdvancedAsync(string deviceUuid, MomentPostRequestDto request)", crmInterface);
        Assert.Contains("PostMomentAdvancedAsync(deviceUuid, MomentPostRequestDto.FromLegacy(content, imageUrls))", ExtractMethod(crm, "PostMomentAsync", "PostMomentAsync(string deviceUuid, string content"));

        var crmAdvanced = ExtractMethod(crm, "PostMomentAdvancedAsync");
        foreach (var required in new[]
        {
            "NormalizeMomentPostRequestForService(request)",
            "MomentPostContentFields(normalizedRequest)",
            "Permissions.MomentOperation.Post",
            "MomentPostAuditMetadata(normalizedRequest)",
            "_deviceCommandService.PostMomentAsync(deviceUuid, normalizedRequest)"
        })
        {
            Assert.Contains(required, crmAdvanced);
        }
        Assert.Contains("extComment", ExtractMethod(crm, "MomentPostContentFields"));
        Assert.Contains("BuildSafeAuditMetadata", ExtractMethod(crm, "MomentPostAuditMetadata"));
        Assert.Contains("labelCount", validator);
        Assert.Contains("friendCount", validator);
        Assert.Contains("notiUserCount", validator);
        Assert.DoesNotContain("[\"attachmentContent\"]", validator);
        Assert.DoesNotContain("[\"poiAddress\"]", validator);

        var hubAdvanced = ExtractMethod(hub, "PostMomentAdvanced");
        Assert.Contains("MomentPostContentFields(normalizedRequest)", hubAdvanced);
        Assert.Contains("Permissions.MomentOperation.Post", hubAdvanced);
        Assert.Contains("MomentPostAuditMetadata(normalizedRequest)", hubAdvanced);
        Assert.Contains("_clientTaskService.SendPostSNSNewsTaskAsync(connectionId, normalizedRequest, taskId)", hubAdvanced);
        Assert.Contains("PostMomentAdvanced", wechatService);

        Assert.Contains("PostMomentAsync(string deviceUuid, MomentPostRequestDto request)", deviceCommand);
        Assert.Contains("_clientTaskService.SendPostSNSNewsTaskAsync(connectionId, validation.Request, taskId)", ExtractMethod(deviceCommand, "PostMomentAsync", "PostMomentAsync(string deviceUuid, MomentPostRequestDto request"));

        Assert.Contains("PostMomentAdvancedAsync(MomentPostRequestDto request)", store);
        Assert.Contains("_service.PostMomentAdvancedAsync(deviceUuid, request)", ExtractMethod(store, "PostMomentAdvancedAsync"));

        foreach (var uiToken in new[]
        {
            "发布朋友圈（62203 高级协议）",
            "_postModel",
            "_attachmentTypes",
            "_visibleTypes",
            "PostMomentAdvanced",
            "Store.PostMomentAdvancedAsync(_postModel)",
            "_postModel.visible.labels",
            "_postModel.notiUsers",
            "_postModel.poi",
            "_postModel.sendSlow"
        })
        {
            Assert.Contains(uiToken, momentsCenter);
        }

        Assert.Contains("MomentPostRequestDto? Payload", controller);
        Assert.Contains("request.Payload ?? MomentPostRequestDto.FromLegacy(request.Content, request.ImageUrls)", controller);
    }

    private static string ExtractMethod(string source, string methodName, string? signatureHint = null)
    {
        var markerIndex = signatureHint == null
            ? source.IndexOf(methodName + "(", StringComparison.Ordinal)
            : source.IndexOf(signatureHint, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        if (signatureHint != null)
        {
            var methodNameIndex = source.IndexOf(methodName, markerIndex, StringComparison.Ordinal);
            if (methodNameIndex >= 0)
            {
                markerIndex = methodNameIndex;
            }
        }

        var start = FindMethodStart(source, markerIndex);
        if (start < 0)
        {
            return string.Empty;
        }

        var braceStart = source.IndexOf('{', markerIndex);
        if (braceStart < 0)
        {
            return string.Empty;
        }

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

        return string.Empty;
    }

    private static int FindMethodStart(string source, int markerIndex)
    {
        var candidates = new[]
        {
            source.LastIndexOf("public ", markerIndex, StringComparison.Ordinal),
            source.LastIndexOf("private ", markerIndex, StringComparison.Ordinal),
            source.LastIndexOf("protected ", markerIndex, StringComparison.Ordinal),
            source.LastIndexOf("internal ", markerIndex, StringComparison.Ordinal)
        };

        return candidates.Where(index => index >= 0).DefaultIfEmpty(-1).Max();
    }

    private static string ReadSource(params string[] parts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("无法定位 SCRM 仓库根目录");
    }
}
