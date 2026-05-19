namespace SCRM.Tests;

/// <summary>
/// 视频号历史导出服务端化、权限校验、字段脱敏与审计防回归测试。
/// <para>导出不能再由前端直接序列化 Store，也不能绕过 finder.export、设备/账号归属与字段级脱敏。</para>
/// </summary>
public sealed class FinderHistoryExportSecurityTests
{
    [Fact]
    public void DtoServiceAndMasking_ShouldDefineServerControlledFinderExport()
    {
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "FinderHistoryExportDto.cs");
        var masking = ReadSource("SCRM.API", "Services", "Security", "SensitiveMaskingService.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        Assert.Contains("public sealed class FinderHistoryExportDto", dto);
        foreach (var member in new[]
        {
            "public bool success",
            "public string message",
            "public string deviceUuid",
            "public int countPerType",
            "public DateTimeOffset exportedAt",
            "public string exportPermission",
            "public string fieldPolicyVersion",
            "public bool masked",
            "public bool rawPayloadIncluded",
            "public bool? successFilter",
            "public long? taskIdFilter",
            "public DateTimeOffset? receivedFrom",
            "public DateTimeOffset? receivedTo",
            "List<FinderMentionNoticeDto> mentionHistory",
            "List<FinderUserPageDto> userPageHistory",
            "List<FinderCommentListDto> commentHistory"
        })
        {
            Assert.Contains(member, dto);
        }

        var canExport = ExtractMethod(masking, "CanExportFinderAsync");
        Assert.Contains("CanExportFinderAsync", masking);
        Assert.Contains("BuildFinderProfileAsync(user)", canExport);
        Assert.Contains("profile.CanReadFinder && profile.CanExportFinder", canExport);

        var exportMethod = ExtractMethod(crm, "ExportFinderHistoryAsync");
        foreach (var required in new[]
        {
            "NormalizeFinderHistoryCount(count)",
            "Permissions.FinderOperation.Export",
            "rawPayloadIncluded = false",
            "CanExportFinderAsync(user)",
            "CanAccessDeviceAsync(user, normalizedDeviceUuid)",
            "CanAccessAccountAsync(user, account.wxid)",
            "LogFinderHistoryExportDeniedAsync",
            "LogFinderHistoryExportedAsync",
            "GetFinderHistoryAsync",
            "MaskFinderMentionsAsync(user, mentionRaw, forExport: true)",
            "MaskFinderUserPagesAsync(user, userPageRaw, forExport: true)",
            "MaskFinderCommentsAsync(user, commentRaw, forExport: true)",
            "missing_finder_export_permission",
            "device_access_denied",
            "account_access_denied"
        })
        {
            Assert.Contains(required, exportMethod);
        }
    }

    [Fact]
    public void AuditService_ShouldLogOnlyFinderExportMetadata()
    {
        var audit = ReadSource("SCRM.API", "Services", "Security", "SensitiveDataAccessAuditService.cs");

        Assert.Contains("ActionFinderHistoryExported", audit);
        Assert.Contains("ActionFinderHistoryExportDenied", audit);
        Assert.Contains("FinderHistoryExported", audit);
        Assert.Contains("FinderHistoryExportDenied", audit);

        var exported = ExtractMethod(audit, "LogFinderHistoryExportedAsync");
        var denied = ExtractMethod(audit, "LogFinderHistoryExportDeniedAsync");

        foreach (var required in new[]
        {
            "dataKind = \"finder-history-export\"",
            "requestedCount",
            "mentionCount",
            "userPageCount",
            "commentCount",
            "totalCount",
            "successFilter",
            "taskIdFilter",
            "receivedFrom",
            "receivedTo",
            "exportPermission = SCRM.Models.Constants.Permissions.FinderOperation.Export",
            "fieldPolicyVersion = \"finder-export-v1\"",
            "masked = true",
            "rawPayloadIncluded = false",
            "queryKeys",
            "LogAuditMessageAsync"
        })
        {
            Assert.Contains(required, exported);
        }

        foreach (var required in new[]
        {
            "dataKind = \"finder-history-export\"",
            "reason = reason?.Trim()",
            "requestedCount",
            "successFilter",
            "taskIdFilter",
            "receivedFrom",
            "receivedTo",
            "exportPermission = SCRM.Models.Constants.Permissions.FinderOperation.Export",
            "queryKeys",
            "LogAuditMessageAsync"
        })
        {
            Assert.Contains(required, denied);
        }

        foreach (var sensitiveField in new[]
        {
            "content",
            "refContent",
            "desc",
            "nonceId",
            "feedAuth",
            "objectNonceId",
            "payloadJson",
            "avatar",
            "cover",
            "thumb",
            "mediaUrl"
        })
        {
            Assert.True(
                exported.IndexOf(sensitiveField, StringComparison.OrdinalIgnoreCase) < 0,
                $"成功导出审计不应记录敏感字段原文：{sensitiveField}");
            Assert.True(
                denied.IndexOf(sensitiveField, StringComparison.OrdinalIgnoreCase) < 0,
                $"拒绝导出审计不应记录敏感字段原文：{sensitiveField}");
        }
    }

    [Fact]
    public void UiStoreAndLegacyApi_ShouldUseServerExportInsteadOfFrontendStoreSerialization()
    {
        var crmInterface = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");
        var finderCenter = ReadSource("SCRM.UI", "Components", "Pages", "FinderCenter.razor");
        var controller = ReadSource("SCRM.API", "Controllers", "ClientTaskController.cs");

        Assert.Contains("Task<FinderHistoryExportDto> ExportFinderHistoryAsync", crmInterface);
        Assert.Contains("ExportFinderHistoryForSelectedDeviceAsync", store);
        Assert.Contains("_service.ExportFinderHistoryAsync", ExtractMethod(store, "ExportFinderHistoryForSelectedDeviceAsync"));

        var pageExport = ExtractMethod(finderCenter, "ExportFinderHistoryAsync");
        Assert.Contains("Store.ExportFinderHistoryForSelectedDeviceAsync", pageExport);
        Assert.Contains("JsonSerializer.Serialize(exportModel", pageExport);
        Assert.DoesNotContain("BuildFinderHistoryExportModel", finderCenter);
        Assert.DoesNotContain("SelectedFinderMentionHistory.ToList()", pageExport);
        Assert.DoesNotContain("SelectedFinderUserPageHistory.ToList()", pageExport);
        Assert.DoesNotContain("SelectedFinderCommentHistory.ToList()", pageExport);

        var apiExport = ExtractMethod(controller, "ExportFinderHistory");
        Assert.Contains("_crmService.ExportFinderHistoryAsync", apiExport);
        Assert.DoesNotContain("GetFinderMentionHistoryAsync", apiExport);
        Assert.DoesNotContain("GetFinderUserPageHistoryAsync", apiExport);
        Assert.DoesNotContain("GetFinderCommentHistoryAsync", apiExport);

        var serviceCallIndex = apiExport.IndexOf("_crmService.ExportFinderHistoryAsync", StringComparison.Ordinal);
        var badRequestIndex = apiExport.IndexOf("BadRequest", StringComparison.Ordinal);
        Assert.True(
            serviceCallIndex >= 0 && badRequestIndex > serviceCallIndex,
            "旧 HTTP 导出接口遇到空 deviceUuid 时，也要先进入 CrmService 写拒绝审计，再返回 BadRequest。");
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var marker = methodName + "(";
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return string.Empty;
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
