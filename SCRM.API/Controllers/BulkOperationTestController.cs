using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.Data;
using SCRM.Entities;
using SCRM.Services;
using EFCore.BulkExtensions;
using System.Diagnostics;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BulkOperationTestController : ControllerBase
    {
        private readonly IBulkOperationService _bulkOperationService;
        private readonly ApplicationDbContext _context;
        private readonly DatabaseInitializationService _dbInitService;
        private readonly ILogger<BulkOperationTestController> _logger;

        public BulkOperationTestController(
            IBulkOperationService bulkOperationService,
            ApplicationDbContext context,
            DatabaseInitializationService dbInitService,
            ILogger<BulkOperationTestController> logger)
        {
            _bulkOperationService = bulkOperationService;
            _context = context;
            _dbInitService = dbInitService;
            _logger = logger;
        }

        /// <summary>
        /// 获取批量操作服务状态
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                // 首先初始化数据库
                await _dbInitService.InitializeDatabaseAsync();

                // 检查数据库连接
                var canConnect = await _context.Database.CanConnectAsync();

                // 获取表记录数量
                var userCount = await _context.BusinessUsers.CountAsync();
                var orderCount = await _context.BusinessOrders.CountAsync();

                return Ok(new
                {
                    Service = "Bulk Operations API",
                    Status = "Running",
                    DatabaseConnected = canConnect,
                    UserCount = userCount,
                    OrderCount = orderCount,
                    Timestamp = DateTime.UtcNow,
                    AvailableEndpoints = new
                    {
                        GetStatus = "GET /api/bulkoperationtest/status",
                        CreateTestUsers = "POST /api/bulkoperationtest/create-test-users",
                        BulkInsertUsers = "POST /api/bulkoperationtest/bulk-insert-users",
                        BulkUpdateUsers = "PUT /api/bulkoperationtest/bulk-update-users",
                        BulkDeleteUsers = "DELETE /api/bulkoperationtest/bulk-delete-users",
                        BulkInsertOrders = "POST /api/bulkoperationtest/bulk-insert-orders",
                        PerformanceBenchmark = "POST /api/bulkoperationtest/performance-benchmark",
                        ClearAllData = "DELETE /api/bulkoperationtest/clear-all-data"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 创建测试用户数据
        /// </summary>
        [HttpPost("create-test-users")]
        public async Task<IActionResult> CreateTestUsers([FromBody] CreateTestUsersRequest request)
        {
            try
            {
                var users = new List<User>();
                var random = new Random();

                for (int i = 1; i <= request.Count; i++)
                {
                    var user = new User
                    {
                        Username = $"user_{i}_{DateTime.Now:yyyyMMddHHmmss}",
                        Email = $"user{i}_{DateTime.Now:yyyyMMddHHmmss}@example.com",
                        FirstName = $"FirstName{i}",
                        LastName = $"LastName{i}",
                        Phone = $"1{random.Next(200, 999)}{random.Next(200, 999)}{random.Next(1000, 9999)}",
                        UserType = request.UserTypes[random.Next(request.UserTypes.Length)],
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    users.Add(user);
                }

                _logger.LogInformation("生成了 {Count} 个测试用户", users.Count);

                return Ok(new
                {
                    Success = true,
                    Message = $"成功生成 {users.Count} 个测试用户",
                    UserCount = users.Count,
                    SampleUser = users.First(),
                    GeneratedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建测试用户时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量插入用户
        /// </summary>
        [HttpPost("bulk-insert-users")]
        public async Task<IActionResult> BulkInsertUsers([FromBody] BulkInsertUsersRequest request)
        {
            try
            {
                var users = new List<User>();
                var random = new Random();

                for (int i = 1; i <= request.Count; i++)
                {
                    var user = new User
                    {
                        Username = $"bulk_user_{i}_{DateTime.Now:yyyyMMddHHmmss}",
                        Email = $"bulk_user{i}_{DateTime.Now:yyyyMMddHHmmss}@example.com",
                        FirstName = $"BulkFirstName{i}",
                        LastName = $"BulkLastName{i}",
                        Phone = $"1{random.Next(200, 999)}{random.Next(200, 999)}{random.Next(1000, 9999)}",
                        UserType = request.UserTypes[random.Next(request.UserTypes.Length)],
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    users.Add(user);
                }

                var stopwatch = Stopwatch.StartNew();
                await _bulkOperationService.BulkInsertAsync(users);
                stopwatch.Stop();

                return Ok(new
                {
                    Success = true,
                    Message = $"批量插入 {users.Count} 个用户成功",
                    UserCount = users.Count,
                    ElapsedTime = $"{stopwatch.Elapsed.TotalSeconds:F2} 秒",
                    Performance = $"{(decimal)users.Count / (decimal)stopwatch.Elapsed.TotalSeconds:F0} 记录/秒",
                    InsertedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量插入用户时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量更新用户
        /// </summary>
        [HttpPut("bulk-update-users")]
        public async Task<IActionResult> BulkUpdateUsers([FromBody] BulkUpdateUsersRequest request)
        {
            try
            {
                // 获取要更新的用户
                var users = await _bulkOperationService.BulkReadAsync<User>(u =>
                    u.Username.StartsWith(request.UserPrefix ?? "bulk_user_"));

                if (!users.Any())
                {
                    return BadRequest(new { Error = "未找到符合条件的用户" });
                }

                // 执行更新
                foreach (var user in users)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    user.IsActive = request.IsActive;
                    if (!string.IsNullOrEmpty(request.UserType))
                    {
                        user.UserType = request.UserType;
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                await _bulkOperationService.BulkUpdateAsync(users);
                stopwatch.Stop();

                return Ok(new
                {
                    Success = true,
                    Message = $"批量更新 {users.Count} 个用户成功",
                    UserCount = users.Count,
                    ElapsedTime = $"{stopwatch.Elapsed.TotalSeconds:F2} 秒",
                    Performance = $"{(decimal)users.Count / (decimal)stopwatch.Elapsed.TotalSeconds:F0} 记录/秒",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新用户时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量删除用户
        /// </summary>
        [HttpDelete("bulk-delete-users")]
        public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkDeleteUsersRequest request)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                if (request.DeleteAll)
                {
                    // 删除所有测试用户
                    await _bulkOperationService.BulkDeleteAsync<User>(u =>
                        u.Username.StartsWith("user_") || u.Username.StartsWith("bulk_user_"));
                }
                else if (!string.IsNullOrEmpty(request.UserPrefix))
                {
                    // 删除指定前缀的用户
                    await _bulkOperationService.BulkDeleteAsync<User>(u =>
                        u.Username.StartsWith(request.UserPrefix));
                }
                else
                {
                    return BadRequest(new { Error = "请指定删除条件" });
                }

                stopwatch.Stop();

                return Ok(new
                {
                    Success = true,
                    Message = "批量删除用户成功",
                    ElapsedTime = $"{stopwatch.Elapsed.TotalSeconds:F2} 秒",
                    DeletedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量删除用户时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量插入订单
        /// </summary>
        [HttpPost("bulk-insert-orders")]
        public async Task<IActionResult> BulkInsertOrders([FromBody] BulkInsertOrdersRequest request)
        {
            try
            {
                // 获取所有用户作为订单的用户ID
                var users = await _context.BusinessUsers.Select(u => u.Id).ToListAsync();

                if (!users.Any())
                {
                    return BadRequest(new { Error = "没有可用的用户，请先创建用户" });
                }

                var orders = new List<Order>();
                var random = new Random();

                for (int i = 1; i <= request.Count; i++)
                {
                    var order = new Order
                    {
                        OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{random.Next(10000, 99999)}",
                        UserId = users[random.Next(users.Count)],
                        ProductName = $"产品 {i}",
                        Category = request.Categories[random.Next(request.Categories.Length)],
                        Amount = (decimal)(random.NextDouble() * 1000 + 50),
                        Quantity = random.Next(1, 10),
                        Status = "Pending",
                        PaymentMethod = request.PaymentMethods[random.Next(request.PaymentMethods.Length)],
                        OrderDate = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                        Notes = $"批量生成的订单 {i}"
                    };
                    orders.Add(order);
                }

                var stopwatch = Stopwatch.StartNew();
                await _bulkOperationService.BulkInsertAsync(orders);
                stopwatch.Stop();

                return Ok(new
                {
                    Success = true,
                    Message = $"批量插入 {orders.Count} 个订单成功",
                    OrderCount = orders.Count,
                    ElapsedTime = $"{stopwatch.Elapsed.TotalSeconds:F2} 秒",
                    Performance = $"{(decimal)orders.Count / (decimal)stopwatch.Elapsed.TotalSeconds:F0} 记录/秒",
                    InsertedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量插入订单时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 性能基准测试
        /// </summary>
        [HttpPost("performance-benchmark")]
        public async Task<IActionResult> PerformanceBenchmark([FromBody] BenchmarkRequest request)
        {
            try
            {
                // 生成测试数据
                var testUsers = new List<User>();
                for (int i = 1; i <= request.RecordCount; i++)
                {
                    testUsers.Add(new User
                    {
                        Username = $"benchmark_user_{i}_{DateTime.Now:yyyyMMddHHmmss}",
                        Email = $"benchmark_user{i}_{DateTime.Now:yyyyMMddHHmmss}@example.com",
                        FirstName = $"BenchmarkFirstName{i}",
                        LastName = $"BenchmarkLastName{i}",
                        Phone = $"1{new Random().Next(200, 999)}{new Random().Next(200, 999)}{new Random().Next(1000, 9999)}",
                        UserType = "Regular",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                var results = new List<object>();

                // 批量插入测试
                var bulkResult = await _bulkOperationService.BenchmarkBulkInsertAsync(testUsers);
                results.Add(new
                {
                    Operation = bulkResult.OperationType,
                    RecordCount = bulkResult.RecordCount,
                    ElapsedTime = $"{bulkResult.ElapsedTime.TotalSeconds:F2}s",
                    RecordsPerSecond = bulkResult.RecordsPerSecond,
                    PerformanceMetrics = bulkResult.PerformanceMetrics
                });

                // 清理测试数据
                await _bulkOperationService.BulkDeleteAsync<User>(u => u.Username.StartsWith("benchmark_user_"));

                return Ok(new
                {
                    Success = true,
                    Message = "性能基准测试完成",
                    TestRecordCount = request.RecordCount,
                    Results = results,
                    TestedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "性能基准测试时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// 清空所有测试数据
        /// </summary>
        [HttpDelete("clear-all-data")]
        public async Task<IActionResult> ClearAllData()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // 先删除订单（因为有外键关系）
                await _bulkOperationService.TruncateAsync<Order>();

                // 再删除用户
                await _bulkOperationService.TruncateAsync<User>();

                stopwatch.Stop();

                return Ok(new
                {
                    Success = true,
                    Message = "所有测试数据已清空",
                    ElapsedTime = $"{stopwatch.Elapsed.TotalSeconds:F2} 秒",
                    ClearedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空测试数据时发生错误");
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }

    // 请求模型
    public class CreateTestUsersRequest
    {
        public int Count { get; set; } = 1000;
        public string[] UserTypes { get; set; } = { "Regular", "Premium", "VIP", "Admin" };
    }

    public class BulkInsertUsersRequest
    {
        public int Count { get; set; } = 1000;
        public string[] UserTypes { get; set; } = { "Regular", "Premium", "VIP" };
    }

    public class BulkUpdateUsersRequest
    {
        public string? UserPrefix { get; set; }
        public bool IsActive { get; set; } = true;
        public string? UserType { get; set; }
    }

    public class BulkDeleteUsersRequest
    {
        public bool DeleteAll { get; set; } = false;
        public string? UserPrefix { get; set; }
    }

    public class BulkInsertOrdersRequest
    {
        public int Count { get; set; } = 1000;
        public string[] Categories { get; set; } = { "Electronics", "Clothing", "Books", "Home", "Sports" };
        public string[] PaymentMethods { get; set; } = { "CreditCard", "PayPal", "BankTransfer", "Cash" };
    }

    public class BenchmarkRequest
    {
        public int RecordCount { get; set; } = 10000;
    }
}