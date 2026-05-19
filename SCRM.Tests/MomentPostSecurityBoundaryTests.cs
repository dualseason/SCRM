using SCRM.API.Services.Core;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.Tests;

/// <summary>
/// 朋友圈高级发圈安全边界防回归测试。
/// <para>覆盖 WeChatId 当前在线账号绑定、Controller 绕过收口、Visible/POI 预校验和回包账号不一致标记。</para>
/// </summary>
public sealed class MomentPostSecurityBoundaryTests
{
    [Fact]
    public void Validator_ShouldBindCurrentWechatAndRejectRiskyPayload()
    {
        var bound = MomentPostRequestValidator.BindAndValidate(
            new MomentPostRequestDto { content = "正常发圈内容" },
            "wxid_A");
        Assert.True(bound.IsValid);
        Assert.Equal("wxid_A", bound.Request.weChatId);

        var mismatch = MomentPostRequestValidator.BindAndValidate(
            new MomentPostRequestDto { weChatId = "wxid_B", content = "正常发圈内容" },
            "wxid_A");
        Assert.False(mismatch.IsValid);
        Assert.Contains("请求微信号与当前设备在线微信号不一致", mismatch.ErrorMessage);

        var emptyVisible = MomentPostRequestValidator.Validate(new MomentPostRequestDto
        {
            content = "范围错误",
            visible = new MomentPostVisibleDto { type = MomentPostVisibleType.WhoInvisible }
        });
        Assert.False(emptyVisible.IsValid);
        Assert.Contains("部分可见/不给谁看必须选择至少一个标签或好友", emptyVisible.ErrorMessage);

        var poiOnlyCoordinate = MomentPostRequestValidator.Validate(new MomentPostRequestDto
        {
            content = "POI 错误",
            poi = new MomentPostPoiDto { lat = 31.2f, lng = 121.4f, poiId = "poi_x" }
        });
        Assert.False(poiOnlyCoordinate.IsValid);
        Assert.Contains("POI 至少需要填写城市或地点名", poiOnlyCoordinate.ErrorMessage);
    }

    [Fact]
    public void ControllerAndServices_ShouldUseUnifiedSecurityPipeline()
    {
        var controller = ReadSource("SCRM.API", "Controllers", "ClientTaskController.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var command = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var clientTask = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var validator = ReadSource("SCRM.API", "Services", "Core", "MomentPostRequestValidator.cs");

        Assert.Contains("public static class MomentPostRequestValidator", validator);
        Assert.Contains("BindAndValidate", validator);
        Assert.Contains("ValidateVisible", validator);
        Assert.Contains("ValidatePoi", validator);
        Assert.Contains("ValidateAttachment", validator);

        var sendPostMoment = ExtractMethod(controller, "SendPostMoment");
        Assert.Contains("var payload = request.Payload ?? MomentPostRequestDto.FromLegacy(request.Content, request.ImageUrls)", sendPostMoment);
        Assert.Contains("_connectionManager.GetConnectionAsync", sendPostMoment);
        Assert.Contains("_crmService.PostMomentAdvancedAsync(deviceUuid, payload)", sendPostMoment);
        Assert.DoesNotContain("_clientTaskService.SendPostSNSNewsTaskAsync", sendPostMoment);
        Assert.Contains("public string DeviceUuid", controller);

        var crmPost = ExtractMethod(crm, "PostMomentAdvancedAsync", "public async Task<TaskResult> PostMomentAdvancedAsync");
        Assert.Contains("GetPrimaryWechatIdAsync(deviceUuid)", crmPost);
        Assert.Contains("MomentPostRequestValidator.BindAndValidate(normalizedRequest, currentWeChatId)", crmPost);
        Assert.Contains("normalizedRequest = validation.Request", crmPost);

        var hubPost = ExtractMethod(hub, "PostMomentAdvanced", "public async Task<TaskResult> PostMomentAdvanced");
        Assert.Contains("GetPrimaryWechatIdAsync(deviceUuid)", hubPost);
        Assert.Contains("MomentPostRequestValidator.BindAndValidate(normalizedRequest, currentWeChatId)", hubPost);
        Assert.Contains("normalizedRequest = validation.Request", hubPost);

        var commandPost = ExtractMethod(command, "PostMomentAsync", "PostMomentAsync(string deviceUuid, MomentPostRequestDto request");
        Assert.Contains("GetRequiredConnectionIdAsync(deviceUuid, nameof(PostMomentAsync))", commandPost);
        Assert.Contains("GetRequiredWeChatIdAsync(deviceUuid, nameof(PostMomentAsync))", commandPost);
        Assert.Contains("MomentPostRequestValidator.BindAndValidate(request, currentWeChatId)", commandPost);
        Assert.Contains("_clientTaskService.SendPostSNSNewsTaskAsync(connectionId, validation.Request, taskId)", commandPost);

        var clientTaskPost = ExtractMethod(clientTask, "SendPostSNSNewsTaskAsync", "SendPostSNSNewsTaskAsync(string connectionId, MomentPostRequestDto? request");
        Assert.Contains("MomentPostRequestValidator.Validate(normalized)", clientTaskPost);
        Assert.Contains("TaskResult.Fail(taskId, validation.ErrorMessage)", clientTaskPost);
    }

    [Fact]
    public void AuditMetadata_ShouldUseHashesWithoutRawSensitiveValues()
    {
        var metadata = MomentPostRequestValidator.BuildSafeAuditMetadata(new MomentPostRequestDto
        {
            clientRequestId = "req_safe_001",
            weChatId = "wxid_secret_owner",
            content = "不要写入审计的正文",
            comment = "不要写入审计的首评",
            attachment = new MomentPostAttachmentDto
            {
                type = MomentPostAttachmentType.ExtLink,
                content = new List<string> { "{\"url\":\"https://secret.example/path\",\"title\":\"秘密标题\"}" }
            },
            visible = new MomentPostVisibleDto
            {
                type = MomentPostVisibleType.WhoVisible,
                labels = new List<string> { "重要客户" },
                friends = new List<string> { "wxid_secret_friend" }
            },
            poi = new MomentPostPoiDto
            {
                city = "秘密城市",
                name = "秘密地点",
                address = "秘密地址",
                poiId = "poi_secret",
                lat = 31.2f,
                lng = 121.4f
            },
            extComment = new List<string> { "不要写入审计的追加评论" },
            notiUsers = new List<string> { "wxid_secret_notice" },
            sendSlow = true
        });

        foreach (var key in new[]
        {
            "payloadHash",
            "contentHash",
            "attachmentHash",
            "visibleTargetsHash",
            "notiUsersHash",
            "effectiveWeChatIdHash",
            "requestedWeChatIdHash",
            "currentWeChatIdHash"
        })
        {
            Assert.True(metadata.TryGetValue(key, out var value), $"缺少审计字段：{key}");
            Assert.StartsWith("sha256:", value?.ToString());
        }

        var serialized = string.Join("\n", metadata.Select(item => $"{item.Key}={item.Value}"));
        foreach (var raw in new[]
        {
            "不要写入审计的正文",
            "不要写入审计的首评",
            "https://secret.example/path",
            "重要客户",
            "wxid_secret_friend",
            "秘密地址",
            "wxid_secret_notice"
        })
        {
            Assert.DoesNotContain(raw, serialized);
        }

        Assert.Contains("req_safe_001", serialized);
        Assert.Contains("attachmentCount=1", serialized);
        Assert.Contains("labelCount=1", serialized);
        Assert.Contains("friendCount=1", serialized);
        Assert.Contains("notiUserCount=1", serialized);
    }

    [Fact]
    public void ResultDtoAndHandler_ShouldMarkAccountMismatch()
    {
        var resultDto = ReadSource("SCRM.SHARED", "Models", "Dtos", "MomentPostResultDto.cs");
        var taskHandler = ReadSource("SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");

        Assert.Contains("public string securityStatus", resultDto);
        Assert.Contains("public bool accountMatched", resultDto);

        foreach (var token in new[]
        {
            "MomentPostRequestValidator.IsSameWechatId(context?.WeChatId, msg.WeChatId)",
            "AccountMismatch",
            "securityStatus = accountMatched ? \"Ok\" : \"AccountMismatch\"",
            "accountMatched = accountMatched",
            "success = momentPostResult.success",
            "朋友圈发布账号不一致，需人工核查"
        })
        {
            Assert.Contains(token, taskHandler);
        }
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
