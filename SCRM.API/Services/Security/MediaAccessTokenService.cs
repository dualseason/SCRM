using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace SCRM.API.Services.Security;

/// <summary>
/// 受控媒体访问短期 Token 服务。
/// <para>不落库，只把上传目录下的相对路径、访问范围和签发用户写入 DataProtection 限时令牌。</para>
/// </summary>
public class MediaAccessTokenService
{
    public const string MediaScope = "media";
    public const string ScreenshotScope = "screenshot";
    public const string CallRecordingScope = "call-recording";

    private static readonly JsonSerializerOptions TokenJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MediaAccessTokenService> _logger;

    public MediaAccessTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<MediaAccessTokenService> logger)
    {
        _protector = dataProtectionProvider
            .CreateProtector("SCRM.MediaAccessToken.v1")
            .ToTimeLimitedDataProtector();
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 获取上传目录请求前缀，例如 uploads。
    /// </summary>
    public string RequestPrefix => (_configuration["FileUploadSettings:RequestUrlPrefix"] ?? "uploads").Trim('/');

    /// <summary>
    /// 创建短期媒体访问令牌。
    /// </summary>
    public MediaAccessTokenIssueResult CreateToken(string relativePath, string scope, string userId, TimeSpan lifetime)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath)
            ?? throw new ArgumentException("媒体相对路径不合法。", nameof(relativePath));
        var normalizedScope = NormalizeScope(scope);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var payload = new MediaAccessTokenPayload
        {
            RelativePath = normalizedRelativePath,
            Scope = normalizedScope,
            UserId = userId,
            IssuedAtUtc = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, TokenJsonOptions);
        var token = _protector.Protect(json, lifetime);
        return new MediaAccessTokenIssueResult(token, expiresAt, normalizedRelativePath, normalizedScope);
    }

    /// <summary>
    /// 验证短期媒体访问令牌。
    /// </summary>
    public bool TryUnprotect(string? token, out MediaAccessTokenPayload payload, out DateTimeOffset expiresAt, out string error)
    {
        payload = new MediaAccessTokenPayload();
        expiresAt = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "token 不能为空。";
            return false;
        }

        try
        {
            var json = _protector.Unprotect(token, out expiresAt);
            var parsed = JsonSerializer.Deserialize<MediaAccessTokenPayload>(json, TokenJsonOptions);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.RelativePath))
            {
                error = "token 内容为空。";
                return false;
            }

            var normalizedRelativePath = NormalizeRelativePath(parsed.RelativePath);
            if (string.IsNullOrWhiteSpace(normalizedRelativePath))
            {
                error = "token 路径不合法。";
                return false;
            }

            parsed.RelativePath = normalizedRelativePath;
            parsed.Scope = NormalizeScope(parsed.Scope);
            payload = parsed;
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "媒体访问 token 解密或过期。");
            error = "token 无效或已过期。";
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "媒体访问 token 解析失败。");
            error = "token 解析失败。";
            return false;
        }
    }

    /// <summary>
    /// 从完整 URL、/uploads/xxx 或 uploads/xxx 中归一化上传目录下的相对路径。
    /// </summary>
    public bool TryNormalizeUploadRelativePath(string? urlOrPath, out string relativePath, out string error)
    {
        relativePath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            error = "媒体 URL 不能为空。";
            return false;
        }

        var text = urlOrPath.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var absoluteUri))
        {
            text = absoluteUri.AbsolutePath;
        }

        var queryIndex = text.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            text = text[..queryIndex];
        }

        text = Uri.UnescapeDataString(text).Replace('\\', '/').TrimStart('/');
        var prefix = RequestPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            error = "上传 URL 前缀未配置。";
            return false;
        }

        if (text.Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "媒体 URL 缺少文件路径。";
            return false;
        }

        if (!text.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            error = $"媒体 URL 必须位于 {prefix}/ 目录下。";
            return false;
        }

        var normalized = NormalizeRelativePath(text[(prefix.Length + 1)..]);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "媒体路径不合法。";
            return false;
        }

        relativePath = normalized;
        return true;
    }

    /// <summary>
    /// 把 token 中的相对路径解析成上传目录下的绝对物理路径，并阻断目录穿越。
    /// </summary>
    public bool TryResolvePhysicalPath(string relativePath, out string physicalPath, out string error)
    {
        physicalPath = string.Empty;
        error = string.Empty;

        var normalized = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "媒体路径不合法。";
            return false;
        }

        var root = GetUploadPhysicalRoot();
        var candidate = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedRoot = Path.GetFullPath(root);
        if (!candidate.StartsWith(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "媒体路径越界。";
            return false;
        }

        physicalPath = candidate;
        return true;
    }

    /// <summary>
    /// 从上传相对路径推断归属对象，便于 Controller 做账号/设备边界校验。
    /// </summary>
    public static MediaAccessTarget InferTarget(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return MediaAccessTarget.None;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Equals("wx", StringComparison.OrdinalIgnoreCase))
        {
            return new MediaAccessTarget(MediaAccessTargetKind.WechatAccount, parts[1]);
        }

        if (parts.Length >= 2 && parts[0].Equals("devices", StringComparison.OrdinalIgnoreCase))
        {
            return new MediaAccessTarget(MediaAccessTargetKind.Device, parts[1]);
        }

        return MediaAccessTarget.None;
    }

    public static string NormalizeScope(string? scope)
    {
        return string.IsNullOrWhiteSpace(scope) ? MediaScope : scope.Trim().ToLowerInvariant();
    }

    public static string? NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Trim().Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part == "." || part == ".." || part.Contains('\0')))
        {
            return null;
        }

        return string.Join('/', parts);
    }

    private string GetUploadPhysicalRoot()
    {
        var storePath = _configuration["FileUploadSettings:StorePath"];
        var uploadStorePath = string.IsNullOrWhiteSpace(storePath)
            ? Path.Combine("wwwroot", RequestPrefix)
            : storePath;

        return Path.GetFullPath(Path.IsPathRooted(uploadStorePath)
            ? uploadStorePath
            : Path.Combine(_environment.ContentRootPath, uploadStorePath));
    }
}

public sealed record MediaAccessTokenIssueResult(string Token, DateTimeOffset ExpiresAt, string RelativePath, string Scope);

public sealed record MediaAccessTarget(MediaAccessTargetKind Kind, string Value)
{
    public static MediaAccessTarget None { get; } = new(MediaAccessTargetKind.None, string.Empty);
}

public enum MediaAccessTargetKind
{
    None = 0,
    WechatAccount = 1,
    Device = 2
}

public sealed class MediaAccessTokenPayload
{
    public string RelativePath { get; set; } = string.Empty;
    public string Scope { get; set; } = MediaAccessTokenService.MediaScope;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
