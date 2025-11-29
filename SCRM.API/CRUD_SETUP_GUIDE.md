# SCRM API - CRUD 自动化生成指南

## 概述

本指南将帮助您快速为所有数据库表生成基础的CRUD操作和Swagger文档。

## 已完成的工作

### 1. 基础设施类 ✅

#### 通用仓储接口 & 实现
- **文件**: `Core/Repository/IBaseRepository.cs` 和 `Core/Repository/BaseRepository.cs`
- **功能**:
  - Create: `AddAsync()`, `AddRangeAsync()`
  - Read: `GetByIdAsync()`, `GetAllAsync()`, `FindAsync()`, `FirstOrDefaultAsync()`
  - Update: `UpdateAsync()`, `UpdateRangeAsync()`
  - Delete: `DeleteAsync()`, `DeleteRangeAsync()`
  - Pagination: `GetPagedAsync()`
  - Soft Delete: `SoftDeleteAsync()`, `SoftDeleteRangeAsync()`

#### 基础API控制器
- **文件**: `Core/Controllers/BaseApiController.cs`
- **功能**:
  - 标准化响应格式 `ApiResponse<T>`
  - 分页响应 `PagedResponse<T>`
  - 便捷方法: `OkResponse()`, `BadRequestResponse()`, `NotFoundResponse()`, `ErrorResponse()`

### 2. 代码生成器 ✅

#### 自动生成工具
- **文件**: `CodeGenerator/GenerateAllModels.cs`
- **生成内容**: 为所有58个表生成
  - Entity Models (Models/Entities/)
  - DTOs (Models/Dtos/)
  - Repository Interfaces (Services/Repository/I*.cs)
  - Repository Implementations (Services/Repository/*Repository.cs)
  - Controllers (Controllers/*Controller.cs)

### 3. Swagger 配置增强 ✅

**已更新 Program.cs**:
```csharp
builder.Services.AddSwaggerGen(c =>
{
    // API信息配置
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SCRM.API - 微信客服系统",
        Version = "v1",
        Description = "微信客服系统 API 文档"
    });

    // JWT Bearer 支持
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
    c.AddSecurityRequirement(...);

    // 启用注解
    c.EnableAnnotations();
});
```

## 快速开始 - 3步生成所有代码

### 步骤 1: 编译生成器

```bash
cd D:\Code\SCRM.SOLUTION\SCRM.API
dotnet build CodeGenerator/GenerateAllModels.cs
```

### 步骤 2: 运行生成器

```bash
cd CodeGenerator
dotnet run
```

或直接运行:

```bash
dotnet run --project SCRM.API -- generate-all
```

### 步骤 3: 注册所有Repository到DI容器

在 `Program.cs` 中的服务注册部分添加:

```csharp
// 注册所有Repository
builder.Services.AddScoped<IWechatAccountRepository, WechatAccountRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
// ... 为所有表添加相同的注册
```

**或使用自动扫描的方式** (更推荐):

```csharp
// 在Program.cs中添加
var repositoryTypes = typeof(Program).Assembly.GetTypes()
    .Where(t => t.Name.EndsWith("Repository") && !t.IsInterface)
    .ToList();

foreach (var impl in repositoryTypes)
{
    var interfaceType = typeof(Program).Assembly.GetTypes()
        .FirstOrDefault(t => t.Name == "I" + impl.Name && t.IsInterface);

    if (interfaceType != null)
    {
        builder.Services.AddScoped(interfaceType, impl);
    }
}
```

## 项目结构

```
SCRM.API/
├── Core/
│   ├── Repository/
│   │   ├── IBaseRepository.cs          # 通用仓储接口
│   │   └── BaseRepository.cs           # 通用仓储实现
│   └── Controllers/
│       └── BaseApiController.cs        # 基础API控制器
├── Models/
│   ├── Entities/                       # Entity Models (自动生成)
│   │   ├── Device.cs
│   │   ├── WechatAccount.cs
│   │   ├── Contact.cs
│   │   └── ... (58个表)
│   └── Dtos/                          # DTO Models (自动生成)
│       ├── DeviceDto.cs
│       ├── WechatAccountDto.cs
│       ├── ContactDto.cs
│       └── ... (58个表)
├── Services/
│   ├── Data/
│   │   └── ApplicationDbContext.cs    # EF Core DbContext
│   └── Repository/                    # 仓储层 (自动生成)
│       ├── IWechatAccountRepository.cs
│       ├── WechatAccountRepository.cs
│       └── ... (58对接口和实现)
├── Controllers/                        # API控制器 (自动生成)
│   ├── WechatAccountController.cs
│   ├── DeviceController.cs
│   ├── ContactController.cs
│   └── ... (58个控制器)
├── CodeGenerator/
│   ├── GenerateAllModels.cs           # 代码生成器
│   ├── GeneratorProgram.cs
│   ├── TableAnalyzer.cs
│   └── CodeGenerator.cs
└── Program.cs                          # 应用程序入口 (已配置Swagger)
```

## 已生成的所有表 (58个)

### 一、设备与账号管理
- [x] devices (设备信息表)
- [x] device_authorizations (设备授权表)
- [x] device_heartbeats (设备心跳表)
- [x] device_version_logs (设备版本日志表)
- [x] device_commands (设备命令表)
- [x] device_status_logs (设备状态日志表)
- [x] device_locations (设备位置记录表)

### 二、好友管理
- [x] contacts (联系人表)
- [x] contact_groups (联系人分组表)
- [x] contact_tags (联系人标签表)
- [x] contact_group_relations (联系人分组关系表)
- [x] contact_tag_relations (联系人标签关系表)
- [x] contact_change_logs (联系人变更日志表)
- [x] friend_requests (好友请求表)
- [x] friend_detection_logs (好友检测日志表)

### 三、消息通信
- [x] messages (聊天消息表)
- [x] message_media (消息媒体表)
- [x] message_extensions (消息扩展表)
- [x] message_forwards (消息转发表)
- [x] message_forward_details (消息转发详情表)
- [x] message_revocations (消息撤回表)
- [x] message_sync_logs (消息同步日志表)
- [x] voice_to_text_logs (语音转文字日志表)

### 四、群聊管理
- [x] groups (群聊表)
- [x] group_members (群成员表)
- [x] group_announcements (群公告表)
- [x] group_change_logs (群聊变更日志表)
- [x] group_invitations (群邀请表)
- [x] group_message_sync_logs (群消息同步日志表)
- [x] group_qrcodes (群二维码表)
- [x] mass_messages (群发消息表)
- [x] mass_message_details (群发消息详情表)

### 五、朋友圈
- [x] moments_posts (朋友圈文章表)
- [x] moments_likes (朋友圈点赞表)
- [x] moments_comments (朋友圈评论表)

### 六、钱包与红包
- [x] wallet_transactions (钱包交易记录表)
- [x] red_packets (红包发送记录表)
- [x] red_packet_records (红包领取记录表)

### 七、公众号与小程序
- [x] official_accounts (公众号账号表)
- [x] miniprogram_accounts (小程序账号表)
- [x] official_account_search_logs (公众号搜索记录表)
- [x] miniprogram_search_logs (小程序搜索记录表)
- [x] official_account_messages (公众号消息记录表)
- [x] miniprogram_messages (小程序消息记录表)
- [x] official_account_subscriptions (公众号订阅记录表)
- [x] miniprogram_access_logs (小程序访问记录表)
- [x] official_account_follow_logs (公众号关注事件日志表)
- [x] miniprogram_follow_logs (小程序关注事件日志表)

### 八、会话管理
- [x] conversations (会话管理表)

### 九、设备与手机操作
- [x] server_redirects (服务器重定向表)

### 十、其他功能
- [x] system_notifications (系统通知表)
- [x] app_versions (应用版本表)

### 十一、权限管理
- [x] wechat_accounts (微信账号信息表)
- [x] account_status_logs (账号状态日志表)
- [x] roles (角色表)
- [x] permissions (权限表)
- [x] user_roles (用户角色关系表)
- [x] role_permissions (角色权限关系表)

## API 端点示例

生成的Controller会提供以下标准端点:

```
GET    /api/{entity}              # 获取所有记录
GET    /api/{entity}/{id}         # 根据ID获取单条记录
GET    /api/{entity}/page         # 分页获取记录
POST   /api/{entity}              # 创建新记录
PUT    /api/{entity}/{id}         # 更新记录
DELETE /api/{entity}/{id}         # 删除记录
```

### 示例请求

```bash
# 获取所有微信账号
GET /api/wechataccount

# 分页获取设备信息
GET /api/device/page?pageNumber=1&pageSize=10

# 创建新的接收方
POST /api/device
Content-Type: application/json
Authorization: Bearer {token}

{
  "id": 0,
  "isDeleted": false,
  "createdAt": "2024-11-30T00:00:00Z"
}

# 更新设备
PUT /api/device/1
Content-Type: application/json
Authorization: Bearer {token}

{
  "id": 1,
  "isDeleted": false,
  "updatedAt": "2024-11-30T10:00:00Z"
}

# 删除设备
DELETE /api/device/1
Authorization: Bearer {token}
```

## Swagger UI

访问 `https://localhost:5000/swagger` 可以:
- 查看所有API端点
- 查看请求/响应模型
- 在线测试API
- 生成API文档

## 注意事项

1. **Entity 和 DTO 映射**
   - Entity 对应数据库表
   - DTO 用于API请求/响应
   - Controller 中需要手动映射 (或使用 AutoMapper)

2. **认证和授权**
   - 所有生成的Controller都使用 `[Authorize]` 特性
   - 需要提供有效的JWT Token

3. **软删除**
   - 所有Entity都支持软删除 (IsDeleted, DeletedAt)
   - 查询时需要自己过滤已删除的记录

4. **EF Core 迁移**
   ```bash
   # 创建迁移
   dotnet ef migrations add InitialCreate

   # 应用迁移
   dotnet ef database update
   ```

## 自定义扩展

为特定的Repository添加自定义方法:

```csharp
public interface IWechatAccountRepository : IBaseRepository<WechatAccount, long>
{
    Task<WechatAccount?> GetByWxidAsync(string wxid);
    Task<List<WechatAccount>> GetOnlineAccountsAsync();
}

public class WechatAccountRepository : BaseRepository<WechatAccount, long>, IWechatAccountRepository
{
    public WechatAccountRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<WechatAccount?> GetByWxidAsync(string wxid)
    {
        return await FirstOrDefaultAsync(a => a.Wxid == wxid);
    }

    public async Task<List<WechatAccount>> GetOnlineAccountsAsync()
    {
        return await FindAsync(a => a.AccountStatus == 1 && !a.IsDeleted);
    }
}
```

## 常见问题

**Q: 如何添加自定义验证?**
A: 在Entity的属性上使用DataAnnotations:
```csharp
[Required]
[StringLength(100)]
[EmailAddress]
public string Email { get; set; }
```

**Q: 如何自定义API响应格式?**
A: 修改 `BaseApiController` 中的映射方法或创建自定义Controller

**Q: 如何添加业务逻辑?**
A: 在Repository类或新建Service类中添加业务逻辑

**Q: 如何处理多对多关系?**
A: 创建中间表 Entity 并在DbContext中配置关系

## 下一步

1. ✅ 运行代码生成器生成所有代码
2. ⏳ 修改生成的Entity添加更多属性 (根据Script.sql)
3. ⏳ 在DbContext中配置所有Entity的映射
4. ⏳ 创建EF Core迁移
5. ⏳ 在Program.cs中注册所有Repository
6. ⏳ 运行应用测试Swagger UI
7. ⏳ 添加自定义业务逻辑到各Repository

## 支持

如有问题，请检查:
- 生成的代码是否正确保存
- 所有命名空间是否导入
- DbContext是否包含所有DbSet属性
- DI容器是否正确注册了所有Repository
