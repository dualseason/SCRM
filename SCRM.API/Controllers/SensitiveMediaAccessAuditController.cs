using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Controllers;

/// <summary>
/// 敏感媒体访问审计查询接口。
/// <para>审计数据来自 system_logs 中 SensitiveMediaAccess 模块，普通用户只返回自己或可访问账号/设备相关记录。</para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/sensitive-media-access-audits")]
public class SensitiveMediaAccessAuditController : ControllerBase
{
    private readonly SensitiveMediaAccessAuditQueryService _queryService;

    public SensitiveMediaAccessAuditController(SensitiveMediaAccessAuditQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// 分页查询敏感媒体访问审计。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SensitiveMediaAccessAuditQueryResultDto>> Query(
        [FromQuery] SensitiveMediaAccessAuditQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.QueryAsync(User, query, cancellationToken);
        if (!result.hasAccess)
        {
            return Forbid();
        }

        return Ok(result);
    }
}
