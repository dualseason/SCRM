using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.Data;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseTestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DatabaseTestController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                // 尝试连接数据库
                var canConnect = await _context.Database.CanConnectAsync();

                if (canConnect)
                {
                    // 获取数据库信息
                    var connection = _context.Database.GetDbConnection();
                    var databaseName = connection.Database;
                    var dataSource = connection.DataSource;

                    return Ok(new
                    {
                        Status = "Success",
                        Message = "成功连接到 PostgreSQL 数据库",
                        DatabaseName = databaseName,
                        DataSource = dataSource,
                        ConnectionTime = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        Status = "Failed",
                        Message = "无法连接到 PostgreSQL 数据库"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = $"数据库连接错误: {ex.Message}",
                    Details = ex.ToString()
                });
            }
        }

        [HttpGet("test-query")]
        public async Task<IActionResult> TestQuery()
        {
            try
            {
                // 执行一个简单的查询来测试数据库功能
                var result = await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                return Ok(new
                {
                    Status = "Success",
                    Message = "数据库查询测试成功",
                    QueryTime = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = $"数据库查询错误: {ex.Message}",
                    Details = ex.ToString()
                });
            }
        }
    }
}