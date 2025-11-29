# SCRM API - CRUD 实现检查清单

## 已完成 ✅

### 核心基础设施
- [x] **IBaseRepository** - 通用仓储接口，定义标准CRUD操作
  - Location: `Core/Repository/IBaseRepository.cs`
  - Features: Create, Read, Update, Delete, Pagination, Soft Delete

- [x] **BaseRepository** - 通用仓储实现类，实现所有基础操作
  - Location: `Core/Repository/BaseRepository.cs`
  - Features: 异步操作、分页、软删除支持

- [x] **BaseApiController** - 基础API控制器
  - Location: `Core/Controllers/BaseApiController.cs`
  - Features: 标准化响应格式、便捷方法、Swagger支持

### 代码生成工具
- [x] **GenerateAllModels.cs** - 自动生成所有Entity、DTO、Repository、Controller
  - Location: `CodeGenerator/GenerateAllModels.cs`
  - Generates: 58 个表的完整代码框架

### Swagger文档配置
- [x] **Program.cs** - Swagger增强配置
  - JWT Bearer 认证支持
  - API 信息和联系方式
  - 注解支持
  - XML 文档支持

### 示例实现
- [x] **WechatAccount.cs** - 微信账号 Entity 示例
  - Location: `Models/Entities/WechatAccount.cs`
  - Shows: 正确的属性映射、数据注解、表映射

### 文档
- [x] **CRUD_SETUP_GUIDE.md** - 快速开始指南
- [x] **IMPLEMENTATION_CHECKLIST.md** - 本文件

---

## 待完成任务 ⏳

### 步骤 1: 运行代码生成器

**目标**: 为所有58个表生成Entity、DTO、Repository和Controller

```bash
# 进入项目目录
cd D:\Code\SCRM.SOLUTION\SCRM.API

# 方式1: 使用批处理文件 (推荐)
generate-crud.bat

# 方式2: 使用PowerShell
.\GenerateCode.ps1

# 方式3: 直接运行 (需要修改GenerateAllModels使其可独立执行)
dotnet run CodeGenerator/GenerateAllModels.cs
```

**预期输出**:
```
✓ 生成完成: Device
✓ 生成完成: Group
✓ 生成完成: WechatAccount
✓ 生成完成: Contact
... (共58个表)
```

**生成的文件**:
- `Models/Entities/*.cs` - 58个Entity类
- `Models/Dtos/*Dto.cs` - 58个DTO类
- `Services/Repository/I*Repository.cs` - 58个Repository接口
- `Services/Repository/*Repository.cs` - 58个Repository实现
- `Controllers/*Controller.cs` - 58个API控制器

### 步骤 2: 更新ApplicationDbContext

**目标**: 添加所有新Entity的DbSet属性

**修改文件**: `Services/Data/ApplicationDbContext.cs`

**添加内容**:
```csharp
public partial class ApplicationDbContext : DbContext
{
    // ==================== 一、设备与账号管理 ====================
    public DbSet<Device> Devices { get; set; }
    public DbSet<WechatAccount> WechatAccounts { get; set; }
    public DbSet<DeviceAuthorization> DeviceAuthorizations { get; set; }
    // ... 为所有表添加

    // ==================== 在 OnModelCreating 中配置 ====================
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ... 为所有Entity添加配置
    }
}
```

**或使用自动扫描** (更简洁):
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 自动配置所有Entity
    var entityTypes = typeof(Program).Assembly.GetTypes()
        .Where(t => t.Namespace == "SCRM.Models.Entities"
                 && !t.IsInterface
                 && t.IsClass)
        .ToList();

    foreach (var entityType in entityTypes)
    {
        modelBuilder.Entity(entityType).Property("CreatedAt")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
```

### 步骤 3: 在Program.cs中注册所有Repository

**目标**: 将所有Repository注入DI容器

**修改文件**: `Program.cs`

**添加到服务注册区域** (在 `builder.Services.Add*` 之间):

**选项A: 手动注册** (清晰但繁琐)
```csharp
// 注册所有Repository
builder.Services.AddScoped<IWechatAccountRepository, WechatAccountRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
// ... 继续注册其他58个Repository
```

**选项B: 自动扫描** (推荐) ⭐
```csharp
// 自动注册所有Repository
var repositoryNamespace = "SCRM.Services.Repository";
var repositoryTypes = typeof(Program).Assembly.GetTypes()
    .Where(t => t.Namespace == repositoryNamespace
             && !t.IsInterface
             && t.Name.EndsWith("Repository"))
    .ToList();

foreach (var impl in repositoryTypes)
{
    var interfaceName = "I" + impl.Name;
    var interfaceType = impl.GetInterfaces()
        .FirstOrDefault(i => i.Name == interfaceName);

    if (interfaceType != null)
    {
        builder.Services.AddScoped(interfaceType, impl);
        Console.WriteLine($"✓ Registered: {interfaceName}");
    }
}
```

### 步骤 4: 创建EF Core迁移

**目标**: 为新表创建数据库迁移

```bash
# 创建初始迁移
dotnet ef migrations add InitialCreate

# 或创建特定命名的迁移
dotnet ef migrations add AddAllTables

# 查看待应用的迁移
dotnet ef migrations list
```

**预期输出**:
```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

**生成的文件**: `Migrations/[timestamp]_InitialCreate.cs`

### 步骤 5: 应用数据库迁移

**目标**: 将迁移应用到PostgreSQL数据库

```bash
# 更新数据库
dotnet ef database update

# 或更新到特定迁移
dotnet ef database update InitialCreate

# 查看数据库状态
dotnet ef database info
```

**预期结果**: 所有表在PostgreSQL中创建成功

### 步骤 6: 为Entity添加具体属性

**当前状态**: 生成的Entity只包含基础属性 (Id, IsDeleted, CreatedAt, UpdatedAt)

**需要做**: 根据Script.sql为每个Entity添加具体列

**示例** (WechatAccount已完成):

**之前**:
```csharp
public class WechatAccount
{
    public long Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**之后** (参考 `Models/Entities/WechatAccount.cs`):
```csharp
public class WechatAccount
{
    [Key]
    [Column("account_id")]
    public long AccountId { get; set; }

    [Required]
    [Column("wxid")]
    [StringLength(100)]
    public string Wxid { get; set; }

    [Column("nickname")]
    [StringLength(100)]
    public string? Nickname { get; set; }

    [Column("mobile_phone")]
    [StringLength(20)]
    public string? MobilePhone { get; set; }

    // ... 更多属性

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
```

**优化**: 可以改进 `GenerateAllModels.cs` 自动从Script.sql提取列信息

### 步骤 7: 相应更新DTO

**当前状态**: DTO对应Entity的所有属性

**需要做**: 根据API需求调整DTO (可选，当前生成的已足够)

### 步骤 8: 测试API

**启动应用**:
```bash
dotnet run
```

**访问Swagger UI**:
```
http://localhost:5000/swagger
或
https://localhost:5001/swagger
```

**测试示例**:

1. **获取所有数据**
   ```bash
   GET /api/wechataccount
   ```

2. **分页查询**
   ```bash
   GET /api/device/page?pageNumber=1&pageSize=10
   ```

3. **创建记录**
   ```bash
   POST /api/wechataccount
   Content-Type: application/json
   Authorization: Bearer {token}

   {
     "accountId": 0,
     "wxid": "test_wxid",
     "nickname": "Test",
     "isDeleted": false
   }
   ```

4. **更新记录**
   ```bash
   PUT /api/wechataccount/1
   ```

5. **删除记录**
   ```bash
   DELETE /api/wechataccount/1
   ```

### 步骤 9: 添加自定义业务逻辑

**在Repository中添加自定义方法**:

```csharp
public interface IWechatAccountRepository : IBaseRepository<WechatAccount, long>
{
    Task<WechatAccount?> GetByWxidAsync(string wxid);
    Task<List<WechatAccount>> GetOnlineAccountsAsync();
    Task<int> UpdateAccountStatusAsync(long accountId, short status);
}

public class WechatAccountRepository : BaseRepository<WechatAccount, long>, IWechatAccountRepository
{
    public WechatAccountRepository(ApplicationDbContext context) : base(context) { }

    public async Task<WechatAccount?> GetByWxidAsync(string wxid)
    {
        return await FirstOrDefaultAsync(a => a.Wxid == wxid && !a.IsDeleted);
    }

    public async Task<List<WechatAccount>> GetOnlineAccountsAsync()
    {
        return await FindAsync(a => a.AccountStatus == 1 && !a.IsDeleted);
    }

    public async Task<int> UpdateAccountStatusAsync(long accountId, short status)
    {
        var account = await GetByIdAsync(accountId);
        if (account == null) return 0;

        account.AccountStatus = status;
        account.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(account);
        return 1;
    }
}
```

**在Controller中使用**:

```csharp
[HttpPatch("{id}/status")]
public async Task<IActionResult> UpdateStatus(long id, [FromQuery] short status)
{
    var result = await _repository.UpdateAccountStatusAsync(id, status);
    return result > 0
        ? OkResponse(null, "更新成功")
        : NotFoundResponse<object>("账号不存在");
}
```

### 步骤 10: 配置权限和认证

**在需要权限保护的Controller中**:

```csharp
[Authorize]                              // 需要认证
[Authorize(Roles = "Admin")]             // 需要Admin角色
[Authorize(Policy = "RequireAdminRole")] // 使用策略
[AllowAnonymous]                         // 允许匿名访问
```

---

## 文件清单

### 已创建的核心文件

| 文件 | 说明 | 状态 |
|------|------|------|
| `Core/Repository/IBaseRepository.cs` | 通用仓储接口 | ✅ |
| `Core/Repository/BaseRepository.cs` | 通用仓储实现 | ✅ |
| `Core/Controllers/BaseApiController.cs` | 基础API控制器 | ✅ |
| `CodeGenerator/GenerateAllModels.cs` | 代码生成器 | ✅ |
| `Models/Entities/WechatAccount.cs` | 示例Entity | ✅ |
| `Services/Data/ApplicationDbContextExtended.cs` | DbContext模板 | ✅ |
| `Program.cs` (修改) | Swagger增强配置 | ✅ |
| `CRUD_SETUP_GUIDE.md` | 快速开始指南 | ✅ |

### 需要生成的文件 (58套)

| 类型 | 数量 | 路径 | 状态 |
|------|------|------|------|
| Entity | 58 | `Models/Entities/*.cs` | ⏳ 待生成 |
| DTO | 58 | `Models/Dtos/*Dto.cs` | ⏳ 待生成 |
| Repository Interface | 58 | `Services/Repository/I*.cs` | ⏳ 待生成 |
| Repository Implementation | 58 | `Services/Repository/*Repository.cs` | ⏳ 待生成 |
| Controller | 58 | `Controllers/*Controller.cs` | ⏳ 待生成 |

---

## 总结时间线

```
当前 ✅
├─ 创建基础设施 (IBaseRepository, BaseRepository, BaseApiController)
├─ 创建代码生成器 (GenerateAllModels.cs)
├─ 增强Swagger配置
└─ 创建文档和指南

下一步 ⏳
├─ 运行代码生成器 (Step 1)
├─ 更新DbContext (Step 2)
├─ 注册Repository (Step 3)
├─ 创建迁移 (Step 4)
├─ 应用迁移 (Step 5)
├─ 完善Entity属性 (Step 6)
├─ 测试API (Step 8)
└─ 添加自定义业务逻辑 (Step 9)

后续优化
└─ 使用AutoMapper简化DTO映射
└─ 添加分布式缓存支持
└─ 集成业务事务处理
└─ 完善错误处理和日志
```

---

## 故障排除

**Q: 代码生成器报错?**
A: 检查:
- Script.sql 文件是否存在
- dotnet版本是否为8.0+
- 命名空间是否正确

**Q: 迁移失败?**
A: 检查:
- 数据库连接字符串是否正确
- PostgreSQL服务是否运行
- 权限是否足够

**Q: Swagger中看不到所有API?**
A: 检查:
- Controller是否在正确的命名空间 (`SCRM.Controllers`)
- Controller是否继承了 `BaseApiController`
- 是否在 `program.cs` 中调用了 `app.MapControllers()`

**Q: API返回401 Unauthorized?**
A: 检查:
- 是否提供了有效的JWT Token
- Token是否过期
- Authorization header格式: `Bearer {token}`

---

## 下一步建议

1. **立即**: 运行代码生成器 (步骤1)
2. **今天**: 完成步骤2-5 (DbContext、迁移)
3. **本周**: 完善Entity属性 (步骤6)
4. **本周**: 添加自定义业务逻辑 (步骤9)
5. **持续**: 根据业务需求扩展功能

---

**最后更新**: 2024-11-30
**版本**: 1.0
