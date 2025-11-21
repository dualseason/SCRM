using Microsoft.EntityFrameworkCore;
using SCRM.Data;
using SCRM.Entities;

namespace SCRM.Services
{
    public class DatabaseInitializationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(ApplicationDbContext context, ILogger<DatabaseInitializationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("开始初始化数据库...");

                // 确保数据库已创建
                await _context.Database.EnsureCreatedAsync();

                // 检查表是否存在，如果不存在则创建
                var userTableExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Users'").ToListAsync();

                if (userTableExists.FirstOrDefault() == 0)
                {
                    await CreateUserTableAsync();
                }

                var orderTableExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Orders'").ToListAsync();

                if (orderTableExists.FirstOrDefault() == 0)
                {
                    await CreateOrderTableAsync();
                }

                _logger.LogInformation("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库初始化失败");
                throw;
            }
        }

        private async Task CreateUserTableAsync()
        {
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS public.""Users"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Username"" VARCHAR(100) NOT NULL UNIQUE,
                    ""Email"" VARCHAR(100) NOT NULL UNIQUE,
                    ""FirstName"" VARCHAR(50),
                    ""LastName"" VARCHAR(50),
                    ""Phone"" VARCHAR(20),
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    ""IsActive"" BOOLEAN DEFAULT true,
                    ""UserType"" VARCHAR(50) DEFAULT 'Regular'
                );

                CREATE INDEX IF NOT EXISTS ""IX_Users_Username"" ON public.""Users""(""Username"");
                CREATE INDEX IF NOT EXISTS ""IX_Users_Email"" ON public.""Users""(""Email"");
            ";

            await _context.Database.ExecuteSqlRawAsync(createTableSql);
            _logger.LogInformation("Users 表创建成功");
        }

        private async Task CreateOrderTableAsync()
        {
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS public.""Orders"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""OrderNumber"" VARCHAR(50) NOT NULL UNIQUE,
                    ""UserId"" INTEGER NOT NULL,
                    ""ProductName"" VARCHAR(100) NOT NULL,
                    ""Category"" VARCHAR(20),
                    ""Amount"" DECIMAL(18,2) NOT NULL,
                    ""Quantity"" INTEGER DEFAULT 1,
                    ""Status"" VARCHAR(20) DEFAULT 'Pending',
                    ""PaymentMethod"" VARCHAR(50),
                    ""OrderDate"" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    ""ShippingDate"" TIMESTAMP WITH TIME ZONE,
                    ""DeliveryDate"" TIMESTAMP WITH TIME ZONE,
                    ""Notes"" TEXT,
                    ""IsDeleted"" BOOLEAN DEFAULT false
                );

                CREATE INDEX IF NOT EXISTS ""IX_Orders_OrderNumber"" ON public.""Orders""(""OrderNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_Orders_UserId"" ON public.""Orders""(""UserId"");
            ";

            await _context.Database.ExecuteSqlRawAsync(createTableSql);
            _logger.LogInformation("Orders 表创建成功");
        }
    }
}