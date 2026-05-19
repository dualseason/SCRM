namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 短期媒体访问 token 响应。
/// <para>前端只使用 Url 播放或下载媒体，不再直接绑定数据库里的永久上传 URL。</para>
/// </summary>
public class MediaAccessTokenDto
{
    /// <summary>
    /// 是否签发成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 失败原因或成功提示。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// DataProtection 生成的限时 token。
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 可直接给 audio/video/img/a 使用的短期代理 URL。
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 上传根目录下的相对路径。
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// 访问范围，例如 media、screenshot、call-recording。
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// token 过期时间。
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public static MediaAccessTokenDto Fail(string message)
    {
        return new MediaAccessTokenDto
        {
            Success = false,
            Message = message
        };
    }

    public static MediaAccessTokenDto Ok(string token, string url, string relativePath, string scope, DateTimeOffset expiresAt)
    {
        return new MediaAccessTokenDto
        {
            Success = true,
            Message = "短期媒体链接已生成。",
            Token = token,
            Url = url,
            RelativePath = relativePath,
            Scope = scope,
            ExpiresAt = expiresAt
        };
    }
}
