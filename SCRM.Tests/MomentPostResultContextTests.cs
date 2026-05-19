namespace SCRM.Tests;

/// <summary>
/// 朋友圈高级发圈结果上下文防回归测试。
/// <para>确保 PostSNSNewsTaskResultNotice 不再只靠字符串解析，而是具备 clientRequestId、CircleId、迟到回包和 UI 状态闭环。</para>
/// </summary>
public sealed class MomentPostResultContextTests
{
    [Fact]
    public void Dto_ShouldExposeClientRequestIdAndStructuredMomentPostResult()
    {
        var requestDto = ReadSource("SCRM.SHARED", "Models", "Dtos", "MomentPostRequestDto.cs");
        var resultDto = ReadSource("SCRM.SHARED", "Models", "Dtos", "MomentPostResultDto.cs");

        Assert.Contains("public string clientRequestId", requestDto);
        Assert.Contains("Guid.NewGuid().ToString(\"N\")", requestDto);
        Assert.Contains("clientRequestId = Guid.NewGuid().ToString(\"N\")", requestDto);

        foreach (var token in new[]
        {
            "public sealed class MomentPostResultDto",
            "public long taskId",
            "public string clientRequestId",
            "public string weChatId",
            "public bool success",
            "public long circleId",
            "public string code",
            "public string errorMessage",
            "public string status",
            "public bool isLate",
            "public bool needSync",
            "public string attachmentType",
            "public int attachmentCount",
            "public string visibleType",
            "public int labelCount",
            "public int friendCount",
            "public int notiUserCount",
            "public int extCommentCount",
            "public bool hasComment",
            "public bool hasPoi",
            "public bool sendSlow",
            "public DateTimeOffset receivedAt"
        })
        {
            Assert.Contains(token, resultDto);
        }
    }

    [Fact]
    public void ClientTaskService_ShouldKeepMomentPostContextBeyondPendingTimeout()
    {
        var clientTask = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var validator = ReadSource("SCRM.API", "Services", "Core", "MomentPostRequestValidator.cs");
        var advancedSend = ExtractMethod(clientTask, "SendPostSNSNewsTaskAsync", "SendPostSNSNewsTaskAsync(string connectionId, MomentPostRequestDto? request");

        foreach (var token in new[]
        {
            "_momentPostTaskContexts",
            "public sealed class MomentPostTaskContext",
            "MomentPostTaskContextTtl = TimeSpan.FromMinutes(30)",
            "RegisterMomentPostTaskContext",
            "TryGetMomentPostTaskContext",
            "RemoveMomentPostTaskContext",
            "PruneExpiredMomentPostTaskContexts",
            "public bool IsTaskPending",
            "ResolveMomentPostTimeoutMs",
            "BuildMomentPostTaskContext"
        })
        {
            Assert.Contains(token, clientTask);
        }

        Assert.Contains("clientRequestId = string.IsNullOrWhiteSpace(request.clientRequestId)", validator);
        Assert.Contains("MomentPostRequestValidator.Validate(normalized)", advancedSend);
        Assert.Contains("RegisterMomentPostTaskContext(BuildMomentPostTaskContext(taskId, normalized))", advancedSend);
        Assert.Contains("ResolveMomentPostTimeoutMs(normalized)", advancedSend);
        Assert.Contains("RemoveMomentPostTaskContext(taskId)", advancedSend);
        Assert.Contains("return needsLongerWait ? 90000 : 60000", clientTask);
    }

    [Fact]
    public void TaskMessageHandler_ShouldBuildStructuredResultAndForwardData()
    {
        var taskHandler = ReadSource("SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");
        var domainEvents = ReadSource("SCRM.SHARED", "Models", "Events", "DomainEvents.cs");
        var forwarding = ReadSource("SCRM.API", "Services", "Events", "EventForwardingService.cs");
        var wechat = ReadSource("SCRM.UI", "Services", "WeChatService.cs");

        foreach (var token in new[]
        {
            "BuildMomentPostResultDto",
            "NormalizePostMomentMessage(postMsg, momentPostResult)",
            "resultData = momentPostResult",
            "_clientTaskService.IsTaskPending(taskIdRequest)",
            "_clientTaskService.TryGetMomentPostTaskContext(taskIdRequest",
            "LatePublished",
            "PublishedNeedSync",
            "LatePublishedNeedSync",
            "needSync = success",
            "AppendResultDataJson(resultMessage, resultData)",
            "new TaskResultReceivedEvent(taskIdRequest, success, eventMessage, string.Empty, deviceUuid)",
            "data = resultData"
        })
        {
            Assert.Contains(token, taskHandler);
        }

        Assert.Contains("public object? data", domainEvents);
        Assert.Contains("object? data = null", domainEvents);
        Assert.Contains("this.data = data", domainEvents);
        Assert.Contains("data = e.data", forwarding);
        Assert.Contains("public object? data", wechat);
    }

    [Fact]
    public void CrmStoreAndMomentsCenter_ShouldShowAndRefreshMomentPostResult()
    {
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");
        var moments = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        foreach (var token in new[]
        {
            "LastMomentPostDeviceUuid",
            "LastMomentPostTaskId",
            "LastMomentPostClientRequestId",
            "LastMomentPostStatus",
            "LastMomentPostMessage",
            "LastMomentPostSuccess",
            "LastMomentPostCircleId",
            "LastMomentPostIsLate",
            "LastMomentPostNeedSync",
            "LastMomentPostAttachmentType",
            "LastMomentPostAttachmentCount",
            "UpdateMomentPostStatus",
            "ClearMomentPostState",
            "ScheduleMomentPostRefresh",
            "TryReadMomentPostResult",
            "TryExtractMomentPostResultFromMessage",
            "StripResultDataJson",
            "IsMomentPostResult"
        })
        {
            Assert.Contains(token, store);
        }

        var handleTaskResult = ExtractMethod(store, "HandleTaskResultReceived", "private void HandleTaskResultReceived");
        Assert.Contains("TryReadMomentPostResult(dto.data", handleTaskResult);
        Assert.Contains("TryExtractMomentPostResultFromMessage(message", handleTaskResult);
        Assert.Contains("UpdateMomentPostStatus(", handleTaskResult);
        Assert.Contains("ScheduleMomentPostRefresh(dto.deviceUuid, momentPostResult)", handleTaskResult);
        Assert.Contains("StripResultDataJson(message)", handleTaskResult);

        var postAdvanced = ExtractMethod(store, "PostMomentAdvancedAsync", "public async Task<TaskResult> PostMomentAdvancedAsync");
        Assert.Contains("request.clientRequestId = Guid.NewGuid().ToString(\"N\")", postAdvanced);
        Assert.Contains("UpdateMomentPostStatus(", postAdvanced);
        Assert.Contains("\"Sending\"", postAdvanced);
        Assert.Contains("\"WaitingClient\"", postAdvanced);
        Assert.Contains("TryReadMomentPostResult(result.data", postAdvanced);
        Assert.Contains("ScheduleMomentPostRefresh(deviceUuid, momentPostResult)", postAdvanced);

        foreach (var token in new[]
        {
            "HasSelectedDeviceMomentPostState",
            "GetMomentPostAlertStyle",
            "LastMomentPostMessage",
            "LastMomentPostTaskId",
            "LastMomentPostClientRequestId",
            "LastMomentPostCircleId",
            "LastMomentPostStatus",
            "LastMomentPostAttachmentType",
            "LastMomentPostNeedSync",
            "LastMomentPostIsLate"
        })
        {
            Assert.Contains(token, moments);
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
