namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 敏感操作审计查询条件。
/// <para>查询对象复用 system_logs 中 SensitiveMediaAccess / SensitiveDataAccess / SensitiveContentRisk 记录，不新增专用审计表。</para>
/// </summary>
public class SensitiveMediaAccessAuditQueryDto
{
    /// <summary>页码，从 1 开始。</summary>
    public int page { get; set; } = 1;

    /// <summary>每页数量，服务端会限制在 1~200。</summary>
    public int pageSize { get; set; } = 50;

    /// <summary>创建时间起点。</summary>
    public DateTime? createdFrom { get; set; }

    /// <summary>创建时间终点。</summary>
    public DateTime? createdTo { get; set; }

    /// <summary>日志级别，例如 Info / Warning。</summary>
    public string level { get; set; } = string.Empty;

    /// <summary>审计模块，例如 SensitiveMediaAccess / SensitiveDataAccess / SensitiveContentRisk。</summary>
    public string module { get; set; } = string.Empty;

    /// <summary>动作，例如 TokenIssued / TokenDenied / ContentBlocked / SensitiveFieldsReturned。</summary>
    public string action { get; set; } = string.Empty;

    /// <summary>访问范围，例如 media / screenshot / call-recording。</summary>
    public string scope { get; set; } = string.Empty;

    /// <summary>签发或打开来源，例如 CreateMediaAccessTokenAsync / Open。</summary>
    public string source { get; set; } = string.Empty;

    /// <summary>操作人 ID。</summary>
    public string operatorId { get; set; } = string.Empty;

    /// <summary>目标 ID，例如 wx:xxx / device:xxx。</summary>
    public string targetId { get; set; } = string.Empty;

    /// <summary>微信账号 wxid。</summary>
    public string accountId { get; set; } = string.Empty;

    /// <summary>设备 UUID。</summary>
    public string deviceUuid { get; set; } = string.Empty;

    /// <summary>上传目录相对路径关键字。</summary>
    public string relativePath { get; set; } = string.Empty;

    /// <summary>详情关键字，例如拒绝原因、CallLogId 或 contentType。</summary>
    public string detail { get; set; } = string.Empty;

    /// <summary>敏感数据类型，例如 SmsRecords / CallLogRecords。</summary>
    public string dataKind { get; set; } = string.Empty;

    /// <summary>综合关键字，用于匹配操作人、目标、动作或审计 JSON。</summary>
    public string keyword { get; set; } = string.Empty;
}

/// <summary>
/// 敏感操作审计分页结果。
/// </summary>
public class SensitiveMediaAccessAuditQueryResultDto
{
    /// <summary>当前用户是否有权读取审计。</summary>
    public bool hasAccess { get; set; } = true;

    /// <summary>结果提示，主要用于无权限或查询失败提示。</summary>
    public string message { get; set; } = string.Empty;

    /// <summary>总记录数。</summary>
    public int totalCount { get; set; }

    /// <summary>当前页码。</summary>
    public int page { get; set; } = 1;

    /// <summary>每页数量。</summary>
    public int pageSize { get; set; } = 50;

    /// <summary>本页数据。</summary>
    public List<SensitiveMediaAccessAuditItemDto> items { get; set; } = new();

    /// <summary>无权限结果。</summary>
    public static SensitiveMediaAccessAuditQueryResultDto Denied(string message)
    {
        return new SensitiveMediaAccessAuditQueryResultDto
        {
            hasAccess = false,
            message = message,
            items = new List<SensitiveMediaAccessAuditItemDto>()
        };
    }
}

/// <summary>
/// 单条敏感操作审计展示项。
/// </summary>
public class SensitiveMediaAccessAuditItemDto
{
    /// <summary>system_logs.id。</summary>
    public long id { get; set; }

    /// <summary>日志级别。</summary>
    public string level { get; set; } = string.Empty;

    /// <summary>审计模块。</summary>
    public string module { get; set; } = string.Empty;

    /// <summary>动作。</summary>
    public string action { get; set; } = string.Empty;

    /// <summary>操作人 ID。</summary>
    public string operatorId { get; set; } = string.Empty;

    /// <summary>目标 ID。</summary>
    public string targetId { get; set; } = string.Empty;

    /// <summary>客户端 IP。</summary>
    public string clientIp { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime createdAt { get; set; }

    /// <summary>访问范围。</summary>
    public string scope { get; set; } = string.Empty;

    /// <summary>上传目录相对路径。</summary>
    public string relativePath { get; set; } = string.Empty;

    /// <summary>归属目标类型。</summary>
    public string targetKind { get; set; } = string.Empty;

    /// <summary>归属目标值。</summary>
    public string targetValue { get; set; } = string.Empty;

    /// <summary>请求传入的微信账号。</summary>
    public string accountId { get; set; } = string.Empty;

    /// <summary>请求传入的设备 UUID。</summary>
    public string deviceUuid { get; set; } = string.Empty;

    /// <summary>审计来源。</summary>
    public string source { get; set; } = string.Empty;

    /// <summary>详情或拒绝原因。</summary>
    public string detail { get; set; } = string.Empty;

    /// <summary>敏感数据类型，例如 SmsRecords / CallLogRecords。</summary>
    public string dataKind { get; set; } = string.Empty;

    /// <summary>敏感内容风控场景，例如 SendMessage / PostMoment。</summary>
    public string scene { get; set; } = string.Empty;

    /// <summary>敏感内容风控等级，例如 block / warn / audit。</summary>
    public string riskLevel { get; set; } = string.Empty;

    /// <summary>命中的敏感词；不包含发送原文。</summary>
    public List<string> matchedWords { get; set; } = new();

    /// <summary>本次返回的敏感字段名，不包含字段原文。</summary>
    public List<string> fields { get; set; } = new();

    /// <summary>本次返回记录数。</summary>
    public int recordCount { get; set; }

    /// <summary>短期 token 的哈希前缀，不包含 token 明文。</summary>
    public string tokenHash { get; set; } = string.Empty;

    /// <summary>签发时间。</summary>
    public DateTimeOffset? issuedAt { get; set; }

    /// <summary>过期时间。</summary>
    public DateTimeOffset? expiresAt { get; set; }

    /// <summary>浏览器或播放器 User-Agent。</summary>
    public string userAgent { get; set; } = string.Empty;

    /// <summary>请求路径。</summary>
    public string requestPath { get; set; } = string.Empty;

    /// <summary>请求查询参数键名。</summary>
    public List<string> queryKeys { get; set; } = new();

    /// <summary>解析错误，正常为空。</summary>
    public string parseError { get; set; } = string.Empty;
}
