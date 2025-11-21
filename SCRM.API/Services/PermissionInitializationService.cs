using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.Constants;
using SCRM.Data;
using SCRM.Models.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class PermissionInitializationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PermissionInitializationService> _logger;

        public PermissionInitializationService(
            ApplicationDbContext context,
            ILogger<PermissionInitializationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializePermissionsAsync()
        {
            _logger.LogInformation("开始初始化权限数据...");

            try
            {
                await InitializePermissionsDataAsync();
                await InitializeRolesAsync();
                await InitializeRolePermissionsAsync();
                await InitializeDefaultUsersAsync();

                _logger.LogInformation("权限数据初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "权限数据初始化失败");
                throw;
            }
        }

        public async Task CreateAdminUserAsync()
        {
            _logger.LogInformation("开始创建管理员用户...");

            try
            {
                await InitializeDefaultUsersAsync();
                _logger.LogInformation("管理员用户创建完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "管理员用户创建失败");
                throw;
            }
        }

        private async Task InitializePermissionsDataAsync()
        {
            if (await _context.Permissions.AnyAsync())
            {
                _logger.LogInformation("权限数据已存在，跳过初始化");
                return;
            }

            var permissions = new[]
            {
                // 用户管理权限
                new Permission { Code = Permissions.User.View, Name = "查看用户", Module = "User", Group = "用户管理", SortOrder = 1, Description = "查看用户列表和详情" },
                new Permission { Code = Permissions.User.Create, Name = "创建用户", Module = "User", Group = "用户管理", SortOrder = 2, Description = "创建新用户" },
                new Permission { Code = Permissions.User.Edit, Name = "编辑用户", Module = "User", Group = "用户管理", SortOrder = 3, Description = "编辑用户信息" },
                new Permission { Code = Permissions.User.Delete, Name = "删除用户", Module = "User", Group = "用户管理", SortOrder = 4, Description = "删除用户" },
                new Permission { Code = Permissions.User.ManageRoles, Name = "管理用户角色", Module = "User", Group = "用户管理", SortOrder = 5, Description = "为用户分配和移除角色" },
                new Permission { Code = Permissions.User.ResetPassword, Name = "重置密码", Module = "User", Group = "用户管理", SortOrder = 6, Description = "重置用户密码" },

                // 角色管理权限
                new Permission { Code = Permissions.Role.View, Name = "查看角色", Module = "Role", Group = "角色管理", SortOrder = 1, Description = "查看角色列表和详情" },
                new Permission { Code = Permissions.Role.Create, Name = "创建角色", Module = "Role", Group = "角色管理", SortOrder = 2, Description = "创建新角色" },
                new Permission { Code = Permissions.Role.Edit, Name = "编辑角色", Module = "Role", Group = "角色管理", SortOrder = 3, Description = "编辑角色信息" },
                new Permission { Code = Permissions.Role.Delete, Name = "删除角色", Module = "Role", Group = "角色管理", SortOrder = 4, Description = "删除角色" },
                new Permission { Code = Permissions.Role.ManagePermissions, Name = "管理角色权限", Module = "Role", Group = "角色管理", SortOrder = 5, Description = "为角色分配和移除权限" },

                // 权限管理权限
                new Permission { Code = Permissions.Permission.View, Name = "查看权限", Module = "Permission", Group = "权限管理", SortOrder = 1, Description = "查看权限列表" },
                new Permission { Code = Permissions.Permission.Create, Name = "创建权限", Module = "Permission", Group = "权限管理", SortOrder = 2, Description = "创建新权限" },
                new Permission { Code = Permissions.Permission.Edit, Name = "编辑权限", Module = "Permission", Group = "权限管理", SortOrder = 3, Description = "编辑权限信息" },
                new Permission { Code = Permissions.Permission.Delete, Name = "删除权限", Module = "Permission", Group = "权限管理", SortOrder = 4, Description = "删除权限" },

                // 客户管理权限
                new Permission { Code = Permissions.Customer.View, Name = "查看客户", Module = "Customer", Group = "客户管理", SortOrder = 1, Description = "查看客户列表和详情" },
                new Permission { Code = Permissions.Customer.Create, Name = "创建客户", Module = "Customer", Group = "客户管理", SortOrder = 2, Description = "创建新客户" },
                new Permission { Code = Permissions.Customer.Edit, Name = "编辑客户", Module = "Customer", Group = "客户管理", SortOrder = 3, Description = "编辑客户信息" },
                new Permission { Code = Permissions.Customer.Delete, Name = "删除客户", Module = "Customer", Group = "客户管理", SortOrder = 4, Description = "删除客户" },
                new Permission { Code = Permissions.Customer.Export, Name = "导出客户", Module = "Customer", Group = "客户管理", SortOrder = 5, Description = "导出客户数据" },
                new Permission { Code = Permissions.Customer.Import, Name = "导入客户", Module = "Customer", Group = "客户管理", SortOrder = 6, Description = "导入客户数据" },
                new Permission { Code = Permissions.Customer.AssignSales, Name = "分配销售", Module = "Customer", Group = "客户管理", SortOrder = 7, Description = "为客户分配销售人员" },

                // 订单管理权限
                new Permission { Code = Permissions.Order.View, Name = "查看订单", Module = "Order", Group = "订单管理", SortOrder = 1, Description = "查看订单列表和详情" },
                new Permission { Code = Permissions.Order.Create, Name = "创建订单", Module = "Order", Group = "订单管理", SortOrder = 2, Description = "创建新订单" },
                new Permission { Code = Permissions.Order.Edit, Name = "编辑订单", Module = "Order", Group = "订单管理", SortOrder = 3, Description = "编辑订单信息" },
                new Permission { Code = Permissions.Order.Delete, Name = "删除订单", Module = "Order", Group = "订单管理", SortOrder = 4, Description = "删除订单" },
                new Permission { Code = Permissions.Order.Cancel, Name = "取消订单", Module = "Order", Group = "订单管理", SortOrder = 5, Description = "取消订单" },
                new Permission { Code = Permissions.Order.Refund, Name = "退款", Module = "Order", Group = "订单管理", SortOrder = 6, Description = "处理订单退款" },
                new Permission { Code = Permissions.Order.Approve, Name = "审批订单", Module = "Order", Group = "订单管理", SortOrder = 7, Description = "审批订单" },
                new Permission { Code = Permissions.Order.Export, Name = "导出订单", Module = "Order", Group = "订单管理", SortOrder = 8, Description = "导出订单数据" },

                // 产品管理权限
                new Permission { Code = Permissions.Product.View, Name = "查看产品", Module = "Product", Group = "产品管理", SortOrder = 1, Description = "查看产品列表和详情" },
                new Permission { Code = Permissions.Product.Create, Name = "创建产品", Module = "Product", Group = "产品管理", SortOrder = 2, Description = "创建新产品" },
                new Permission { Code = Permissions.Product.Edit, Name = "编辑产品", Module = "Product", Group = "产品管理", SortOrder = 3, Description = "编辑产品信息" },
                new Permission { Code = Permissions.Product.Delete, Name = "删除产品", Module = "Product", Group = "产品管理", SortOrder = 4, Description = "删除产品" },
                new Permission { Code = Permissions.Product.ManageInventory, Name = "管理库存", Module = "Product", Group = "产品管理", SortOrder = 5, Description = "管理产品库存" },
                new Permission { Code = Permissions.Product.ManagePrice, Name = "管理价格", Module = "Product", Group = "产品管理", SortOrder = 6, Description = "管理产品价格" },

                // 系统管理权限
                new Permission { Code = Permissions.System.Dashboard, Name = "系统仪表盘", Module = "System", Group = "系统管理", SortOrder = 1, Description = "访问系统仪表盘" },
                new Permission { Code = Permissions.System.Settings, Name = "系统设置", Module = "System", Group = "系统管理", SortOrder = 2, Description = "管理系统设置" },
                new Permission { Code = Permissions.System.Logs, Name = "系统日志", Module = "System", Group = "系统管理", SortOrder = 3, Description = "查看系统日志" },
                new Permission { Code = Permissions.System.Backup, Name = "数据备份", Module = "System", Group = "系统管理", SortOrder = 4, Description = "执行数据备份" },
                new Permission { Code = Permissions.System.Maintenance, Name = "系统维护", Module = "System", Group = "系统管理", SortOrder = 5, Description = "执行系统维护" },

                // 报表权限
                new Permission { Code = Permissions.Report.View, Name = "查看报表", Module = "Report", Group = "报表管理", SortOrder = 1, Description = "查看报表" },
                new Permission { Code = Permissions.Report.Create, Name = "创建报表", Module = "Report", Group = "报表管理", SortOrder = 2, Description = "创建新报表" },
                new Permission { Code = Permissions.Report.Export, Name = "导出报表", Module = "Report", Group = "报表管理", SortOrder = 3, Description = "导出报表数据" },
                new Permission { Code = Permissions.Report.Sales, Name = "销售报表", Module = "Report", Group = "报表管理", SortOrder = 4, Description = "查看销售报表" },
                new Permission { Code = Permissions.Report.Customer, Name = "客户报表", Module = "Report", Group = "报表管理", SortOrder = 5, Description = "查看客户报表" },
                new Permission { Code = Permissions.Report.Finance, Name = "财务报表", Module = "Report", Group = "报表管理", SortOrder = 6, Description = "查看财务报表" },

                // 消息权限
                new Permission { Code = Permissions.Message.Send, Name = "发送消息", Module = "Message", Group = "消息管理", SortOrder = 1, Description = "发送消息" },
                new Permission { Code = Permissions.Message.Receive, Name = "接收消息", Module = "Message", Group = "消息管理", SortOrder = 2, Description = "接收消息" },
                new Permission { Code = Permissions.Message.Broadcast, Name = "广播消息", Module = "Message", Group = "消息管理", SortOrder = 3, Description = "广播消息" },
                new Permission { Code = Permissions.Message.Template, Name = "消息模板", Module = "Message", Group = "消息管理", SortOrder = 4, Description = "管理消息模板" }
            };

            await _context.Permissions.AddRangeAsync(permissions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("已初始化 {Count} 个权限", permissions.Length);
        }

        private async Task InitializeRolesAsync()
        {
            if (await _context.Roles.AnyAsync())
            {
                _logger.LogInformation("角色数据已存在，跳过初始化");
                return;
            }

            var roles = new[]
            {
                new Role { Name = Roles.SuperAdmin, Description = "超级管理员，拥有系统所有权限" },
                new Role { Name = Roles.Admin, Description = "系统管理员，拥有大部分管理权限" },
                new Role { Name = Roles.Manager, Description = "部门经理，拥有部门内管理权限" },
                new Role { Name = Roles.Sales, Description = "销售人员，拥有客户和订单管理权限" },
                new Role { Name = Roles.CustomerService, Description = "客服人员，拥有客户服务相关权限" },
                new Role { Name = Roles.User, Description = "普通用户，拥有基本查看权限" }
            };

            await _context.Roles.AddRangeAsync(roles);
            await _context.SaveChangesAsync();

            _logger.LogInformation("已初始化 {Count} 个角色", roles.Length);
        }

        private async Task InitializeRolePermissionsAsync()
        {
            if (await _context.RolePermissions.AnyAsync())
            {
                _logger.LogInformation("角色权限关系已存在，跳过初始化");
                return;
            }

            var allPermissions = await _context.Permissions.ToListAsync();
            var allRoles = await _context.Roles.ToListAsync();

            var rolePermissions = new List<RolePermission>();

            // 超级管理员拥有所有权限
            var superAdminRole = allRoles.FirstOrDefault(r => r.Name == Roles.SuperAdmin);
            if (superAdminRole != null)
            {
                foreach (var permission in allPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = superAdminRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            // 管理员权限（除了系统维护外的所有权限）
            var adminRole = allRoles.FirstOrDefault(r => r.Name == Roles.Admin);
            if (adminRole != null)
            {
                var adminPermissions = allPermissions.Where(p =>
                    p.Code != Permissions.System.Maintenance &&
                    !p.Code.Contains("SuperAdmin"));

                foreach (var permission in adminPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            // 经理权限
            var managerRole = allRoles.FirstOrDefault(r => r.Name == Roles.Manager);
            if (managerRole != null)
            {
                var managerPermissions = allPermissions.Where(p =>
                    p.Module == "Customer" || p.Module == "Order" || p.Module == "Product" ||
                    p.Module == "Report" || p.Module == "Message" ||
                    p.Code == Permissions.System.Dashboard ||
                    p.Code == Permissions.User.View);

                foreach (var permission in managerPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = managerRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            // 销售人员权限
            var salesRole = allRoles.FirstOrDefault(r => r.Name == Roles.Sales);
            if (salesRole != null)
            {
                var salesPermissions = allPermissions.Where(p =>
                    (p.Module == "Customer" && p.Code != Permissions.Customer.Delete && p.Code != Permissions.Customer.AssignSales) ||
                    (p.Module == "Order" && p.Code != Permissions.Order.Delete && p.Code != Permissions.Order.Approve && p.Code != Permissions.Order.Refund) ||
                    (p.Module == "Product" && p.Code == Permissions.Product.View) ||
                    (p.Module == "Report" && (p.Code == Permissions.Report.Sales || p.Code == Permissions.Report.Customer)) ||
                    (p.Module == "Message" && (p.Code == Permissions.Message.Send || p.Code == Permissions.Message.Receive)));

                foreach (var permission in salesPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = salesRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            // 客服权限
            var customerServiceRole = allRoles.FirstOrDefault(r => r.Name == Roles.CustomerService);
            if (customerServiceRole != null)
            {
                var csPermissions = allPermissions.Where(p =>
                    (p.Module == "Customer" && p.Code != Permissions.Customer.Delete) ||
                    (p.Module == "Order" && (p.Code == Permissions.Order.View || p.Code == Permissions.Order.Edit || p.Code == Permissions.Order.Cancel)) ||
                    (p.Module == "Product" && p.Code == Permissions.Product.View) ||
                    (p.Module == "Report" && (p.Code == Permissions.Report.Customer || p.Code == Permissions.Report.Sales)) ||
                    (p.Module == "Message" && (p.Code == Permissions.Message.Send || p.Code == Permissions.Message.Receive || p.Code == Permissions.Message.Template)));

                foreach (var permission in csPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = customerServiceRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            // 普通用户权限（只有基本查看权限）
            var userRole = allRoles.FirstOrDefault(r => r.Name == Roles.User);
            if (userRole != null)
            {
                var userPermissions = allPermissions.Where(p =>
                    p.Code == Permissions.Customer.View ||
                    p.Code == Permissions.Order.View ||
                    p.Code == Permissions.Product.View ||
                    p.Code == Permissions.Message.Receive);

                foreach (var permission in userPermissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = userRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            await _context.RolePermissions.AddRangeAsync(rolePermissions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("已初始化 {Count} 个角色权限关系", rolePermissions.Count);
        }

        private async Task InitializeDefaultUsersAsync()
        {
            if (await _context.IdentityUsers.AnyAsync())
            {
                _logger.LogInformation("用户数据已存在，跳过初始化");
                return;
            }

            // 创建默认超级管理员用户
            var adminUser = new Models.Identity.User
            {
                UserName = "admin",
                Email = "admin@scrm.com",
                FirstName = "系统",
                LastName = "管理员",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), // 使用BCrypt密码哈希
                IsActive = true,
                EmailConfirmed = true
            };

            await _context.IdentityUsers.AddAsync(adminUser);
            await _context.SaveChangesAsync();

            // 分配超级管理员角色
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.SuperAdmin);
            if (adminRole != null)
            {
                await _context.UserRoles.AddAsync(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("已创建默认管理员用户: {UserName}", adminUser.UserName);
        }
    }
}