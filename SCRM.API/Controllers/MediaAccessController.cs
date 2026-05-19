using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Services.Security;
using SCRM.Services.Data;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Controllers;

/// <summary>
/// 受控媒体访问控制器。
/// <para>用于把上传目录下的永久 URL 换成短期 token，并通过代理端点读取文件。</para>
/// </summary>
[ApiController]
[Route("api/media-access")]
public class MediaAccessController : ControllerBase
{
    private readonly MediaAccessTokenService _tokenService;
    private readonly SensitiveMaskingService _sensitiveMaskingService;
    private readonly AccountAccessGuard _accountAccessGuard;
    private readonly SensitiveMediaAccessAuditService _auditService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MediaAccessController> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public MediaAccessController(
        MediaAccessTokenService tokenService,
        SensitiveMaskingService sensitiveMaskingService,
        AccountAccessGuard accountAccessGuard,
        SensitiveMediaAccessAuditService auditService,
        ApplicationDbContext db,
        ILogger<MediaAccessController> logger)
    {
        _tokenService = tokenService;
        _sensitiveMaskingService = sensitiveMaskingService;
        _accountAccessGuard = accountAccessGuard;
        _auditService = auditService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 为上传目录下的媒体 URL 换取短期访问 token。
    /// <para>聊天媒体默认要求 message.view_raw；截图要求设备归属；录音建议走 call-recordings/{id}/token。</para>
    /// </summary>
    [Authorize]
    [HttpPost("token")]
    public async Task<ActionResult<MediaAccessTokenDto>> CreateToken([FromBody] MediaAccessTokenRequest request)
    {
        if (!_tokenService.TryNormalizeUploadRelativePath(request.Url, out var relativePath, out var error))
        {
            await _auditService.LogTokenDeniedAsync(
                User,
                request.Scope,
                null,
                error,
                nameof(CreateToken),
                request.AccountId,
                request.DeviceUuid,
                HttpContext);
            return BadRequest(new { success = false, message = error });
        }

        var scope = MediaAccessTokenService.NormalizeScope(request.Scope);
        if (!await CanIssueTokenForRelativePathAsync(relativePath, scope, request.DeviceUuid, request.AccountId))
        {
            await _auditService.LogTokenDeniedAsync(
                User,
                scope,
                relativePath,
                "权限或资源归属校验失败。",
                nameof(CreateToken),
                request.AccountId,
                request.DeviceUuid,
                HttpContext);
            return Forbid();
        }

        var response = CreateResponse(relativePath, scope, request.ExpiresMinutes);
        await _auditService.LogTokenIssuedAsync(
            User,
            response,
            nameof(CreateToken),
            request.AccountId,
            request.DeviceUuid,
            HttpContext);
        return Ok(response);
    }

    /// <summary>
    /// 为通话录音签发短期播放 token。
    /// </summary>
    [Authorize]
    [HttpPost("call-recordings/{id:int}/token")]
    public async Task<ActionResult<MediaAccessTokenDto>> CreateCallRecordingToken(int id, [FromBody] MediaAccessTokenOptions? options = null)
    {
        var record = await _db.CallLogRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.id == id);
        if (record == null)
        {
            await _auditService.LogTokenDeniedAsync(
                User,
                MediaAccessTokenService.CallRecordingScope,
                null,
                "通话记录不存在。",
                nameof(CreateCallRecordingToken),
                accountId: null,
                deviceUuid: null,
                HttpContext);
            return NotFound(new { success = false, message = "通话记录不存在。" });
        }

        if (!await _accountAccessGuard.CanAccessAccountAsync(User, record.ownerWxid))
        {
            _logger.LogWarning(
                "签发录音 token 被拒绝：账号越权。User={User}, CallLogId={CallLogId}, OwnerWxid={OwnerWxid}",
                AccountAccessGuard.GetUserIdCandidates(User).FirstOrDefault() ?? User.Identity?.Name,
                id,
                record.ownerWxid);
            await _auditService.LogTokenDeniedAsync(
                User,
                MediaAccessTokenService.CallRecordingScope,
                null,
                "账号越权。",
                nameof(CreateCallRecordingToken),
                record.ownerWxid,
                null,
                HttpContext);
            return Forbid();
        }

        var profile = await _sensitiveMaskingService.BuildProfileAsync(User);
        if (!profile.CanViewCallRecordUrl)
        {
            _logger.LogWarning(
                "签发录音 token 被拒绝：缺少录音查看权限。User={User}, CallLogId={CallLogId}",
                AccountAccessGuard.GetUserIdCandidates(User).FirstOrDefault() ?? User.Identity?.Name,
                id);
            await _auditService.LogTokenDeniedAsync(
                User,
                MediaAccessTokenService.CallRecordingScope,
                null,
                "缺少录音查看权限。",
                nameof(CreateCallRecordingToken),
                record.ownerWxid,
                null,
                HttpContext);
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(record.recordUrl))
        {
            await _auditService.LogTokenDeniedAsync(
                User,
                MediaAccessTokenService.CallRecordingScope,
                null,
                "该通话记录没有录音 URL。",
                nameof(CreateCallRecordingToken),
                record.ownerWxid,
                null,
                HttpContext);
            return BadRequest(new { success = false, message = "该通话记录没有录音 URL。" });
        }

        if (!_tokenService.TryNormalizeUploadRelativePath(record.recordUrl, out var relativePath, out var error))
        {
            await _auditService.LogTokenDeniedAsync(
                User,
                MediaAccessTokenService.CallRecordingScope,
                null,
                error,
                nameof(CreateCallRecordingToken),
                record.ownerWxid,
                null,
                HttpContext);
            return BadRequest(new { success = false, message = error });
        }

        var response = CreateResponse(relativePath, MediaAccessTokenService.CallRecordingScope, options?.ExpiresMinutes);
        await _auditService.LogTokenIssuedAsync(
            User,
            response,
            nameof(CreateCallRecordingToken),
            record.ownerWxid,
            null,
            HttpContext,
            detail: $"CallLogId={id}");
        return Ok(response);
    }

    /// <summary>
    /// 使用短期 token 读取媒体文件。
    /// <para>该端点允许匿名访问，因为 token 本身已经限时签名；不要把长期 URL 放到 token 内。</para>
    /// </summary>
    [AllowAnonymous]
    [HttpGet("open")]
    public async Task<IActionResult> Open([FromQuery] string token)
    {
        if (!_tokenService.TryUnprotect(token, out var payload, out var expiresAt, out var error))
        {
            await _auditService.LogTokenOpenDeniedAsync(
                payload: null,
                reason: error,
                source: nameof(Open),
                httpContext: HttpContext,
                token: token);
            return Unauthorized(new { success = false, message = error });
        }

        if (!_tokenService.TryResolvePhysicalPath(payload.RelativePath, out var physicalPath, out error))
        {
            await _auditService.LogTokenOpenDeniedAsync(
                payload,
                error,
                nameof(Open),
                HttpContext,
                token);
            return BadRequest(new { success = false, message = error });
        }

        if (!System.IO.File.Exists(physicalPath))
        {
            await _auditService.LogTokenOpenDeniedAsync(
                payload,
                "媒体文件不存在或已清理。",
                nameof(Open),
                HttpContext,
                token);
            return NotFound(new { success = false, message = "媒体文件不存在或已清理。" });
        }

        if (!_contentTypeProvider.TryGetContentType(physicalPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        Response.Headers.CacheControl = "private, max-age=60";
        Response.Headers["X-Media-Token-Expires"] = expiresAt.ToString("O");
        _logger.LogInformation(
            "短期媒体 token 已使用。Scope={Scope}, RelativePath={RelativePath}, UserId={UserId}",
            payload.Scope,
            payload.RelativePath,
            payload.UserId);
        await _auditService.LogTokenOpenedAsync(
            payload,
            expiresAt,
            nameof(Open),
            HttpContext,
            contentType);

        return PhysicalFile(physicalPath, contentType, enableRangeProcessing: true);
    }

    private async Task<bool> CanIssueTokenForRelativePathAsync(string relativePath, string scope, string? deviceUuid, string? accountId)
    {
        var profile = await _sensitiveMaskingService.BuildProfileAsync(User);
        if (scope == MediaAccessTokenService.CallRecordingScope)
        {
            if (!profile.CanViewCallRecordUrl)
            {
                return false;
            }
        }
        else if (scope != MediaAccessTokenService.ScreenshotScope && !profile.CanViewMessageRaw)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(deviceUuid)
            && !await _accountAccessGuard.CanAccessDeviceAsync(User, deviceUuid))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(accountId)
            && !await _accountAccessGuard.CanAccessAccountAsync(User, accountId))
        {
            return false;
        }

        var inferredTarget = MediaAccessTokenService.InferTarget(relativePath);
        return inferredTarget.Kind switch
        {
            MediaAccessTargetKind.WechatAccount => await _accountAccessGuard.CanAccessAccountAsync(User, inferredTarget.Value),
            MediaAccessTargetKind.Device => await _accountAccessGuard.CanAccessDeviceAsync(User, inferredTarget.Value),
            _ => AccountAccessGuard.IsAdmin(User)
        };
    }

    private MediaAccessTokenDto CreateResponse(string relativePath, string scope, int? requestedExpiresMinutes)
    {
        var expiresMinutes = Math.Clamp(requestedExpiresMinutes ?? 5, 1, 30);
        var userId = AccountAccessGuard.GetUserIdCandidates(User).FirstOrDefault()
            ?? User.Identity?.Name
            ?? string.Empty;
        var issued = _tokenService.CreateToken(relativePath, scope, userId, TimeSpan.FromMinutes(expiresMinutes));
        var openUrl = Url.ActionLink(nameof(Open), values: new { token = issued.Token }) ?? $"/api/media-access/open?token={Uri.EscapeDataString(issued.Token)}";

        return new MediaAccessTokenDto
        {
            Success = true,
            Message = "短期媒体链接已生成。",
            Token = issued.Token,
            Url = openUrl,
            RelativePath = issued.RelativePath,
            Scope = issued.Scope,
            ExpiresAt = issued.ExpiresAt
        };
    }
}

public sealed class MediaAccessTokenRequest : MediaAccessTokenOptions
{
    /// <summary>
    /// 原始上传 URL、/uploads/... 或 uploads/...。
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 媒体访问范围：media/screenshot/call-recording。
    /// </summary>
    public string Scope { get; set; } = MediaAccessTokenService.MediaScope;

    /// <summary>
    /// 可选设备 UUID；传入时会额外校验设备归属。
    /// </summary>
    public string? DeviceUuid { get; set; }

    /// <summary>
    /// 可选微信账号 wxid；传入时会额外校验账号归属。
    /// </summary>
    public string? AccountId { get; set; }
}

public class MediaAccessTokenOptions
{
    /// <summary>
    /// token 有效分钟数，范围 1..30，默认 5。
    /// </summary>
    public int? ExpiresMinutes { get; set; }
}
