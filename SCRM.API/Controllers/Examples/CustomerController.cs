using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCRM.Attributes;
using SCRM.Constants;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.Controllers.Examples
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        // 基本权限检查 - 需要客户查看权限
        [HttpGet]
        [RequirePermission(Permissions.Customer.View)]
        public IActionResult GetCustomers()
        {
            var customers = new[]
            {
                new { Id = 1, Name = "张三", Email = "zhangsan@example.com" },
                new { Id = 2, Name = "李四", Email = "lisi@example.com" }
            };

            return Ok(new { Success = true, Data = customers });
        }

        // 创建权限检查 - 需要客户创建权限
        [HttpPost]
        [RequirePermission(Permissions.Customer.Create)]
        public IActionResult CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            // 模拟创建客户逻辑
            return Ok(new { Success = true, Message = "客户创建成功", Data = new { Id = 3, Name = request.Name } });
        }

        // 编辑权限检查 - 需要客户编辑权限
        [HttpPut("{id}")]
        [RequirePermission(Permissions.Customer.Edit)]
        public IActionResult UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
        {
            return Ok(new { Success = true, Message = $"客户 {id} 更新成功" });
        }

        // 删除权限检查 - 需要客户删除权限
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.Customer.Delete)]
        public IActionResult DeleteCustomer(int id)
        {
            return Ok(new { Success = true, Message = $"客户 {id} 删除成功" });
        }

        // 导出权限检查 - 需要客户导出权限
        [HttpGet("export")]
        [RequirePermission(Permissions.Customer.Export)]
        public IActionResult ExportCustomers()
        {
            return Ok(new { Success = true, Message = "客户导出成功", Data = new { FileUrl = "/downloads/customers.xlsx" } });
        }

        // 分配销售权限检查 - 需要客户分配销售权限
        [HttpPost("{id}/assign-sales")]
        [RequirePermission(Permissions.Customer.AssignSales)]
        public IActionResult AssignSales(int id, [FromBody] AssignSalesRequest request)
        {
            return Ok(new { Success = true, Message = $"客户 {id} 分配销售成功" });
        }

        // 多权限检查 - 需要任意一个权限
        [HttpGet("dashboard")]
        [RequirePermissions(Permissions.Customer.View, Permissions.Report.Sales)]
        public IActionResult GetCustomerDashboard()
        {
            var dashboard = new
            {
                TotalCustomers = 150,
                NewCustomersThisMonth = 25,
                ActiveCustomers = 120,
                TopCustomers = new[]
                {
                    new { Name = "公司A", Amount = 50000 },
                    new { Name = "公司B", Amount = 35000 }
                }
            };

            return Ok(new { Success = true, Data = dashboard });
        }

        // 角色权限检查 - 需要特定角色
        [HttpGet("vip")]
        [RequireRole(Roles.Admin, Roles.Manager)]
        public IActionResult GetVipCustomers()
        {
            var vipCustomers = new[]
            {
                new { Id = 1, Name = "VIP客户1", Level = "钻石" },
                new { Id = 2, Name = "VIP客户2", Level = "白金" }
            };

            return Ok(new { Success = true, Data = vipCustomers });
        }

        // 复合权限检查 - 需要客户查看权限 AND 销售角色
        [HttpGet("my-assigned")]
        [RequirePermission(Permissions.Customer.View)]
        [RequireRole(Roles.Sales)]
        public IActionResult GetMyAssignedCustomers()
        {
            // 获取当前用户ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            var assignedCustomers = new[]
            {
                new { Id = 1, Name = "我的客户1", AssignedTo = userName },
                new { Id = 2, Name = "我的客户2", AssignedTo = userName }
            };

            return Ok(new { Success = true, Data = assignedCustomers });
        }

        // 无需权限的公共接口
        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult GetPublicInfo()
        {
            return Ok(new { Success = true, Message = "这是公开信息，无需权限" });
        }
    }

    public class CreateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class AssignSalesRequest
    {
        public int SalesUserId { get; set; }
    }
}