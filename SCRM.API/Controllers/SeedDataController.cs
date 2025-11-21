using Microsoft.AspNetCore.Mvc;
using SCRM.Services;
using System.Threading.Tasks;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/seeddata")]
    public class SeedDataController : ControllerBase
    {
        private readonly PermissionInitializationService _permissionInitService;
        private readonly ILogger<SeedDataController> _logger;

        public SeedDataController(
            PermissionInitializationService permissionInitService,
            ILogger<SeedDataController> logger)
        {
            _permissionInitService = permissionInitService;
            _logger = logger;
        }

        [HttpPost("seed-permissions")]
        public async Task<IActionResult> SeedPermissions()
        {
            try
            {
                await _permissionInitService.InitializePermissionsAsync();
                return Ok(new { Success = true, Message = "权限数据初始化成功" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "权限数据初始化失败");
                return StatusCode(500, new { Success = false, Message = "权限数据初始化失败" });
            }
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminUser()
        {
            try
            {
                await _permissionInitService.CreateAdminUserAsync();
                return Ok(new { Success = true, Message = "管理员用户创建成功" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "管理员用户创建失败");
                return StatusCode(500, new { Success = false, Message = "管理员用户创建失败" });
            }
        }
    }
}